namespace DiscordWhoIs.Services
{
    using DiscordWhoIs.Configuration.Models;
    using DiscordWhoIs.Databases.DataModels;
    using DiscordWhoIs.Databases.Interfaces;
    using DiscordWhoIs.Models;
    using DiscordWhoIs.Regexs;
    using HtmlAgilityPack;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Ao3FicFeedService
    {
        private static readonly Lock _lastRequestLock = new();

        private readonly HttpClient _http;
        private readonly IPersistentCache _cache;
        private readonly ILogger<Ao3FicFeedService> _logger;
        private readonly IAliasStore _aliasStore;
        private readonly FandomConfiguration _fandomConfig;
        private readonly CacheConfiguration _cacheConfig;
        private readonly Ao3Configuration _ao3Config;
        private readonly SemaphoreSlim _Ao3Lock;

        private static DateTime _lastAo3RequestUtc = DateTime.MinValue;

        // Timeout tracking
        private int _timeoutCount = 0;
        private DateTime? _lastTimeoutUtc = null;
        private string? _lastTimeoutMessage = null;
        private readonly Lock _timeoutLock = new();

        public Ao3FicFeedService(
            IHttpClientFactory httpClientFactory,
            IPersistentCache cache,
            ILogger<Ao3FicFeedService> logger,
            IAliasStore aliasStore,
            FandomConfiguration fandomConfig,
            CacheConfiguration cacheConfig,
            Ao3Configuration ao3Config)
        {
            _http = httpClientFactory.CreateClient("Ao3");
            _cache = cache;
            _logger = logger;
            _aliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));
            _fandomConfig = fandomConfig;
            _cacheConfig = cacheConfig;
            _ao3Config = ao3Config;

            _Ao3Lock = new SemaphoreSlim(_ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3ConcurrencyLimit);

            _logger.LogInformation("Ao3FicFeedService constructed. " +
                "ConcurrencyLimit={Limit} MinDelayMs={MinDelay} BackoffMs={Backoff} MaxRetries={MaxRetries}",
                _ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3MinimumDelayMs.TotalMilliseconds, _ao3Config.Ao3BackoffMs, _ao3Config.MaxRetries);
        }

        #region Public Methods
        /// <summary>
        /// Returns an object that lets callers know if AO3 requests are currently being throttled.
        /// </summary>
        /// <returns></returns>
        public Ao3ThrottleStatus GetThrottleStatus()
        {
            DateTime last;
            lock (_lastRequestLock)
            {
                last = _lastAo3RequestUtc;
            }

            var now = DateTime.UtcNow;
            var since = now - last;

            var minDelay = _ao3Config.Ao3MinimumDelayMs;
            TimeSpan until = TimeSpan.Zero;

            if (since < minDelay)
                until = minDelay - since;

            // semaphore.CurrentCount shows available concurrency slots
            int availableSlots = _Ao3Lock.CurrentCount;

            return new Ao3ThrottleStatus(
                until > TimeSpan.Zero || availableSlots <= 0,
                since,
                until,
                availableSlots
            );
        }

        /// <summary>
        /// Returns an object that provides timeout statistics for AO3 requests.
        /// </summary>
        /// <returns></returns>
        public Ao3TimeoutStatus GetTimeoutStatus()
        {
            lock (_timeoutLock)
            {
                bool degraded = _timeoutCount >= 3 &&
                                _lastTimeoutUtc.HasValue &&
                                (DateTime.UtcNow - _lastTimeoutUtc.Value) < TimeSpan.FromMinutes(10);

                return new Ao3TimeoutStatus(
                    _timeoutCount,
                    _lastTimeoutUtc,
                    _lastTimeoutMessage,
                    degraded
                );
            }
        }

        public string ResolveAo3Username(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var trimmed = input.Trim();
            if (_aliasStore.TryResolve(trimmed, out var real))
            {
                _logger.LogDebug("Alias resolved: {Alias} -> {Resolved}", trimmed, real);
                return real;
            }

            return trimmed;
        }

        public async Task<Ao3ResponseStatus> GetUserFicsAsync(string user)
        {
            var resolvedUser = ResolveAo3Username(user);

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("Resolved alias {Alias} -> {RealUser}", user, resolvedUser);

            var cacheKeyResolved = $"{resolvedUser}";
            if (_cache.TryGetValue(cacheKeyResolved, out var cached))
            {
                _logger.LogInformation("Persistent cache hit for {User}", resolvedUser);

                return new Ao3ResponseStatus()
                {
                    IsSuccessful = true,
                    Fics = cached!
                };
            }

            _logger.LogInformation("Persistent cache miss for {User} - scraping Ao3", resolvedUser);
            var results = await ScrapeAllPagesConcurrentAsync(resolvedUser);

            if (results != null && results.Fics.Any())
            {
                _cache.SetAsync(cacheKeyResolved, results.Fics, _cacheConfig.ExpirationInHours);
            }

            return results;
        }
        #endregion

        #region Scraping & Parsing
        private async Task<Ao3ResponseStatus> ScrapeAllPagesConcurrentAsync(string user)
        {
            _logger.LogInformation("Begin ScrapeAllPagesConcurrentAsync for {User}", user);

            var responseStatus = new Ao3ResponseStatus();
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={_fandomConfig.TargetFandom}";
            _logger.LogDebug("Scraping base URL: {Url}", baseUrl);

            var firstPageHtml = await SafeGetStringAo3Async(baseUrl);
            if (firstPageHtml is null)
            {
                _logger.LogWarning("First page returned NULL HTML for user {User}", user);
                responseStatus.Fics = Enumerable.Empty<FicInfo>();
                responseStatus.IsSuccessful = false;
                return responseStatus;
            }

            if (firstPageHtml.Length < 5000)
            {
                _logger.LogWarning("First page HTML size unusually small ({Bytes} bytes) for user {User}", firstPageHtml.Length, user);
            }

            var parsedFirst = ParseFicsFromHtml(firstPageHtml);
            _logger.LogDebug("Parsed {Count} fics from first page for {User}", parsedFirst.Count, user);

            var results = new List<FicInfo>(parsedFirst);
            if (results.Count >= 10)
            {
                _logger.LogInformation("First page already returned {Count} fics, stopping early", results.Count);
                responseStatus.IsSuccessful = true;
                responseStatus.Fics = results.Take(10);
                return responseStatus;
            }

            // Determine total pages
            _logger.LogDebug("Determining page count for {User}", user);
            var doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(firstPageHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HtmlAgilityPack failed to load first page HTML for {User}", user);
            }

            int totalPages = 1;
            var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
            if (pagination != null)
            {
                try
                {
                    var last = pagination.SelectNodes(".//li/a")
                        ?.Select(a => a.GetAttributeValue("href", ""))
                        .LastOrDefault(h => h.Contains("page="));

                    if (!string.IsNullOrEmpty(last))
                    {
                        var m = Ao3Regex.PageCountRegex().Match(last);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out var p))
                            totalPages = p;
                        else
                            _logger.LogWarning("Failed to parse page count from '{Value}' for {User}", last, user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while extracting pagination for {User}", user);
                }
            }

            if (totalPages <= 1)
            {
                _logger.LogInformation("Only one page of results for {User}", user);
                responseStatus.IsSuccessful = true;
                responseStatus.Fics = results.Take(10);
                return responseStatus;
            }

            var pageNumbers = Enumerable.Range(2, totalPages - 1).ToList();
            _logger.LogInformation("Fetching {Count} more pages concurrently for {User}: {Pages}", pageNumbers.Count, user, string.Join(", ", pageNumbers));

            var pageTasks = pageNumbers.Select(page => FetchPageAsync(baseUrl, page)).ToList();

            IEnumerable<FicInfo>[] pageResults;
            try
            {
                pageResults = await Task.WhenAll(pageTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while awaiting page fetch tasks for {User}", user);
                pageResults = Array.Empty<IEnumerable<FicInfo>>();
            }

            for (int i = 0; i < pageResults.Length; i++)
            {
                var page = pageNumbers.ElementAtOrDefault(i);
                var pageFics = pageResults[i] ?? Enumerable.Empty<FicInfo>();
                _logger.LogDebug("Page {Page} returned {Count} fics for {User}", page, pageFics.Count(), user);

                results.AddRange(pageFics);
                if (results.Count >= 10)
                {
                    _logger.LogInformation("Collected {Count} fics (>=10) for {User}, stopping early", results.Count, user);
                    break;
                }
            }

            _logger.LogInformation("Scraping complete for {User}. Total collected: {Count}", user, results.Count);
            responseStatus.IsSuccessful = true;
            responseStatus.Fics = results.Take(10);
            return responseStatus;
        }

        private async Task<IEnumerable<FicInfo>> FetchPageAsync(string baseUrl, int page)
        {
            var url = $"{baseUrl}&page={page}";
            _logger.LogDebug("Begin FetchPageAsync for page {Page}. URL: {Url}", page, url);

            for (int attempt = 1; attempt <= _ao3Config.MaxRetries; attempt++)
            {
                _logger.LogDebug("Fetch attempt {Attempt}/{Max} for page {Page}", attempt, _ao3Config.MaxRetries, page);
                var html = await SafeGetStringAo3Async(url);

                if (!string.IsNullOrEmpty(html))
                {
                    if (html.Length < 3000) RecordTimeout(null, url);
                    return ParseFicsFromHtml(html);
                }

                if (attempt < _ao3Config.MaxRetries)
                {
                    var backoff = TimeSpan.FromMilliseconds(_ao3Config.Ao3BackoffMs.Milliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogWarning("[Ao3] Retry {Attempt}/{Max} for page {Page} after {Delay}ms", attempt, _ao3Config.MaxRetries, page, backoff.TotalMilliseconds);
                    await Task.Delay(backoff);
                }
            }

            _logger.LogError("[Ao3] Failed to fetch page {Page} after {Max} attempts (url: {Url})", page, _ao3Config.MaxRetries, url);
            return Enumerable.Empty<FicInfo>();
        }

        private async Task<string?> SafeGetStringAo3Async(string url)
        {
            var waitSw = Stopwatch.StartNew();
            _logger.LogDebug("[Ao3Lock] Waiting to enter semaphore for URL: {Url}", url);
            await _Ao3Lock.WaitAsync();
            waitSw.Stop();
            _logger.LogDebug("[Ao3Lock] Entered semaphore after {WaitMs}ms for URL: {Url}", waitSw.ElapsedMilliseconds, url);

            try
            {
                // Enforce minimum delay
                int delay;
                lock (_lastRequestLock)
                {
                    var elapsed = DateTime.UtcNow - _lastAo3RequestUtc;
                    delay = Math.Max(0, (int)(_ao3Config.Ao3MinimumDelayMs.TotalMilliseconds - elapsed.TotalMilliseconds));
                }

                if (delay > 0)
                {
                    _logger.LogInformation("[Ao3] Waiting {Delay}ms to respect minimum delay between requests", delay);
                    await Task.Delay(delay);
                }

                var sw = Stopwatch.StartNew();
                _logger.LogInformation("[Ao3-HTTP] GET {Url}", url);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    if ((int)resp.StatusCode == 429)
                    {
                        _logger.LogWarning("[Ao3] 429 Too Many Requests for {Url}; backing off {Ms}ms", url, _ao3Config.Ao3BackoffMs);
                        RecordTimeout(null, url);
                        await Task.Delay(_ao3Config.Ao3BackoffMs);
                        return null;
                    }
                    else if ((int)resp.StatusCode == 443)
                    {
                        _logger.LogWarning("[Ao3] 443 Connection Closed for {Url}; backing off {Ms}ms", url, _ao3Config.Ao3BackoffMs);
                        RecordTimeout(null, url);
                        await Task.Delay(_ao3Config.Ao3BackoffMs);
                        return null;
                    }

                    var text = await resp.Content.ReadAsStringAsync(cts.Token);

                    // Update last request timestamp AFTER request
                    lock (_lastRequestLock) { _lastAo3RequestUtc = DateTime.UtcNow; }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        RecordTimeout(null, url);
                        return null;
                    }

                    if (text.Length < 2000)
                        RecordTimeout(null, url);

                    return text;
                }
                catch (TaskCanceledException tce)
                {
                    RecordTimeout(tce, url);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
                catch (Exception ex)
                {
                    RecordTimeout(ex, url);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
            }
            finally
            {
                try
                {
                    _Ao3Lock.Release();
                }
                catch (SemaphoreFullException)
                {
                    _logger.LogWarning("[Ao3Lock] Semaphore already released for URL: {Url}", url);
                }
            }
        }

        private List<FicInfo> ParseFicsFromHtml(string? html)
        {
            if (html is null) return new List<FicInfo>();

            var doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HtmlAgilityPack failed to load HTML"); return new List<FicInfo>();
            }

            var nodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'work')]|//li[contains(@class,'work blurb group')]");
            if (nodes == null || nodes.Count == 0) return new List<FicInfo>();

            var results = new List<FicInfo>();
            foreach (var n in nodes)
            {
                try
                {
                    var titleNode = n.SelectSingleNode(".//h4/a") ?? n.SelectSingleNode(".//h4/span/a");
                    if (titleNode == null) continue;

                    var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                    var href = titleNode.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrWhiteSpace(href)) continue;

                    var full = href.StartsWith("http") ? href : "https://archiveofourown.org" + href;

                    results.Add(new FicInfo { Title = title, Url = full });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception parsing fic node");
                }
            }

            return results;
        }
        #endregion

        #region Timeout Helper
        private void RecordTimeout(Exception? ex, string url)
        {
            lock (_timeoutLock)
            {
                _timeoutCount++;
                _lastTimeoutUtc = DateTime.UtcNow;
                _lastTimeoutMessage = ex?.Message ?? "Timeout / empty response";
            }

            _logger.LogWarning(ex,
                "[Ao3Timeout] Timeout recorded for {Url}. Total so far: {Count}. Last message: {Msg}",
                url, _timeoutCount, _lastTimeoutMessage);
        }
        #endregion
    }
}
