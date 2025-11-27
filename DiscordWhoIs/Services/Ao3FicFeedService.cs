namespace DiscordWhoIs.Services
{
    using DiscordWhoIs.Configuration.Models;
    using DiscordWhoIs.Databases.DataModels;
    using DiscordWhoIs.Databases.Interfaces;
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
        private readonly SemaphoreSlim _Ao3Lock; // concurrency control

        private static DateTime _lastAo3RequestUtc = DateTime.MinValue;

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

            _logger.LogInformation("Ao3FicFeedService constructed. ConcurrencyLimit={Limit} MinDelayMs={MinDelay} BackoffMs={Backoff} MaxRetries={MaxRetries}",
                _ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3MinimumDelayMs.TotalMilliseconds, _ao3Config.Ao3BackoffMs, _ao3Config.MaxRetries);
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

        public async Task<IEnumerable<FicInfo>> GetUserFicsAsync(string user)
        {
            var resolvedUser = ResolveAo3Username(user);

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("Resolved alias {Alias} -> {RealUser}", user, resolvedUser);

            var cacheKeyResolved = $"{resolvedUser}";
            if (_cache.TryGetValue(cacheKeyResolved, out var cached))
            {
                _logger.LogInformation("Persistent cache hit for {User}", resolvedUser);
                return cached!;
            }

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
            {
                var cacheKeyAlias = $"{user}";
                if (_cache.TryGetValue(cacheKeyAlias, out var aliasCached))
                {
                    _logger.LogInformation("Persistent cache hit for alias {Alias} (mapped to {Resolved})", user, resolvedUser);
                    _cache.SetAsync(cacheKeyResolved, aliasCached!, _cacheConfig.ExpirationInHours);
                    return aliasCached!;
                }
            }

            _logger.LogInformation("Persistent cache miss for {User} - scraping Ao3", resolvedUser);
            var results = await ScrapeAllPagesConcurrentAsync(resolvedUser);

            _cache.SetAsync(cacheKeyResolved, results, _cacheConfig.ExpirationInHours);
            return results;
        }

        private async Task<IEnumerable<FicInfo>> ScrapeAllPagesConcurrentAsync(string user)
        {
            _logger.LogInformation("Begin ScrapeAllPagesConcurrentAsync for {User}", user);

            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={_fandomConfig.TargetFandom}";
            _logger.LogDebug("Scraping base URL: {Url}", baseUrl);

            var firstPageHtml = await SafeGetStringAo3Async(baseUrl);
            if (firstPageHtml is null)
            {
                _logger.LogWarning("First page returned NULL HTML for user {User}", user);
                return Enumerable.Empty<FicInfo>();
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
                return results.Take(10);
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
                // but continue — parser method already ran successfully once
            }

            var totalPages = 1;
            var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
            if (pagination == null)
            {
                _logger.LogWarning("No pagination block found for user {User}; assuming 1 page", user);
            }
            else
            {
                _logger.LogDebug("Pagination block found, extracting page count for {User}", user);
                try
                {
                    var last = pagination.SelectNodes(".//li/a")
                        ?.Select(a => a.GetAttributeValue("href", ""))
                        .LastOrDefault(h => h.Contains("page="));

                    if (string.IsNullOrEmpty(last))
                    {
                        _logger.LogWarning("Pagination block present but no page links contain 'page=' for {User}", user);
                    }
                    else
                    {
                        var m = Ao3Regex.PageCountRegex().Match(last);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out var p))
                        {
                            totalPages = p;
                            _logger.LogInformation("Detected {Pages} pages for {User}", totalPages, user);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse page count from '{Value}' for {User}", last, user);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while extracting pagination for {User}", user);
                }
            }

            if (totalPages <= 1)
            {
                _logger.LogInformation("Only one page found for {User}", user);
                return results.Take(10);
            }

            // Fetch remaining pages concurrently
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
                // Task.WhenAll may throw if a task faulted; individual FetchPageAsync handles its own errors and returns empty, so this is defensive
                _logger.LogError(ex, "Unexpected exception while awaiting page fetch tasks for {User}", user);
                pageResults = Array.Empty<IEnumerable<FicInfo>>();
            }

            for (int i = 0; i < pageResults.Length; i++)
            {
                var page = pageNumbers.ElementAtOrDefault(i);
                var pageFics = pageResults[i] ?? Enumerable.Empty<FicInfo>();
                _logger.LogDebug("Page {Page} returned {Count} fics for {User}", page, pageFics.Count(), user);

                if (!pageFics.Any())
                {
                    _logger.LogWarning("Page {Page} returned NO fics for {User}", page, user);
                }

                results.AddRange(pageFics);
                if (results.Count >= 10)
                {
                    _logger.LogInformation("Collected {Count} fics (>=10) for {User}, stopping early", results.Count, user);
                    break;
                }
            }

            _logger.LogInformation("Scraping complete for {User}. Total collected: {Count}", user, results.Count);

            return results.Take(10);
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
                    if (html.Length < 3000)
                    {
                        _logger.LogWarning("Fetched HTML for page {Page} is very small ({Bytes} bytes). Possibly blocked or incomplete response.", page, html.Length);
                    }

                    var parsed = ParseFicsFromHtml(html);
                    _logger.LogDebug("Parsed {Count} fics from page {Page}", parsed.Count, page);
                    return parsed;
                }

                if (attempt < _ao3Config.MaxRetries)
                {
                    // exponential backoff based on attempt (note: formula preserved but logged)
                    var backoff = TimeSpan.FromMilliseconds(_ao3Config.Ao3BackoffMs.Milliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogWarning("[Ao3] Retry {Attempt}/{Max} for page {Page} after {Delay}ms (backoff)", attempt, _ao3Config.MaxRetries, page, backoff.TotalMilliseconds);
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
                // Enforce minimal delay between requests
                int delay = 0;
                lock (_lastRequestLock)
                {
                    var elapsed = DateTime.UtcNow - _lastAo3RequestUtc;
                    delay = Math.Max(0, (int)(_ao3Config.Ao3MinimumDelayMs.TotalMilliseconds - elapsed.TotalMilliseconds));
                }

                if (delay > 0)
                {
                    _logger.LogInformation("[Ao3] Waiting {Delay}ms to respect minimum delay between requests (robots policy)", delay);
                    await Task.Delay(delay);
                }
                else
                {
                    _logger.LogDebug("[Ao3] No minimum delay required before this request");
                }

                lock (_lastRequestLock)
                {
                    _lastAo3RequestUtc = DateTime.UtcNow;
                }

                var sw = Stopwatch.StartNew();
                _logger.LogInformation("[Ao3-HTTP] GET {Url}", url);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    if ((int)resp.StatusCode == 429)
                    {
                        _logger.LogWarning("[Ao3] 429 Too Many Requests for URL {Url} — backing off {Ms}ms", url, _ao3Config.Ao3BackoffMs);
                        await Task.Delay(_ao3Config.Ao3BackoffMs);
                        return null;
                    }

                    _logger.LogInformation("[Ao3-HTTP] Received {Status} for {Url} after {Elapsed}ms", resp.StatusCode, url, sw.ElapsedMilliseconds);

                    var text = await resp.Content.ReadAsStringAsync(cts.Token);

                    _logger.LogInformation("[Ao3-HTTP] Body read {Bytes} bytes for {Url} in {Elapsed}ms", text?.Length ?? 0, url, sw.ElapsedMilliseconds);

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogWarning("[Ao3] Response body is empty or whitespace for {Url}", url);
                        return null;
                    }

                    // detect likely HTML error pages (e.g., Cloudflare challenge / 403 page)
                    if (text.Length < 2000)
                    {
                        _logger.LogWarning("[Ao3] Response body appears small ({Bytes} bytes) for {Url}. This may indicate an error page or blocking.", text.Length, url);
                    }

                    return text;
                }
                catch (TaskCanceledException tce)
                {
                    _logger.LogWarning(tce, "[Ao3] Request timed out for {Url} — backing off {Ms}ms", url, _ao3Config.Ao3BackoffMs);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Ao3] HTTP request failed for {Url} — backing off {Ms}ms", url, _ao3Config.Ao3BackoffMs);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
            }
            finally
            {
                try
                {
                    _Ao3Lock.Release();
                    _logger.LogDebug("[Ao3Lock] Released semaphore for URL: {Url}", url);
                }
                catch (SemaphoreFullException)
                {
                    _logger.LogWarning("[Ao3Lock] Attempted to release semaphore when it was already fully released for URL: {Url}", url);
                }
            }
        }

        private List<FicInfo> ParseFicsFromHtml(string? html)
        {
            if (html is null)
            {
                _logger.LogWarning("ParseFicsFromHtml called with null HTML");
                return new List<FicInfo>();
            }

            _logger.LogDebug("Begin parsing HTML, length={Length}", html.Length);

            var doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HtmlAgilityPack failed to load HTML in ParseFicsFromHtml");
                return new List<FicInfo>();
            }

            var nodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'work')]|//li[contains(@class,'work blurb group')]");
            if (nodes == null || nodes.Count == 0)
            {
                _logger.LogWarning("No fic nodes found in HTML (node count = {Count})", nodes?.Count ?? 0);
                return new List<FicInfo>();
            }

            _logger.LogDebug("Found {Count} fic nodes", nodes.Count);

            var results = new List<FicInfo>();
            foreach (var n in nodes)
            {
                try
                {
                    var titleNode = n.SelectSingleNode(".//h4/a") ?? n.SelectSingleNode(".//h4/span/a");
                    if (titleNode == null)
                    {
                        _logger.LogDebug("Skipping a node with no title anchor");
                        continue;
                    }

                    var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                    var href = titleNode.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        _logger.LogDebug("Skipping a node with empty href for title '{Title}'", title);
                        continue;
                    }

                    var full = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? href
                        : "https://archiveofourown.org" + href;

                    results.Add(new FicInfo
                    {
                        Title = title,
                        Url = full
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while parsing a fic node. Continuing with others.");
                }
            }

            _logger.LogDebug("ParseFicsFromHtml extracted {Count} fics", results.Count);
            return results;
        }
    }
}
