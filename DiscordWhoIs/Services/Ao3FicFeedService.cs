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
    using System.Linq;
    using System.Text.RegularExpressions;
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
        }

        public string ResolveAo3Username(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var trimmed = input.Trim();
            if (_aliasStore.TryResolve(trimmed, out var real)) return real;
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
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={_fandomConfig.TargetFandom}";
            var firstPageHtml = await SafeGetStringAo3Async(baseUrl);
            if (firstPageHtml is null) return [];

            var results = new List<FicInfo>(ParseFicsFromHtml(firstPageHtml));
            if (results.Count >= 10) return results.Take(10);

            // Determine total pages
            var doc = new HtmlDocument();
            doc.LoadHtml(firstPageHtml);

            var totalPages = 1;
            var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
            if (pagination != null)
            {
                var last = pagination.SelectNodes(".//li/a")
                    ?.Select(a => a.GetAttributeValue("href", ""))
                    .LastOrDefault(h => h.Contains("page="));

                if (last != null)
                {
                    var m = Ao3Regex.PageCountRegex().Match(last);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var p))
                        totalPages = p;
                }
            }

            _logger.LogInformation("User {User} has {Pages} pages", user, totalPages);

            if (totalPages <= 1) return results.Take(10);

            // Fetch remaining pages concurrently
            var pageNumbers = Enumerable.Range(2, totalPages - 1);
            var pageTasks = pageNumbers.Select(page => FetchPageAsync(baseUrl, page)).ToList();

            var pageResults = await Task.WhenAll(pageTasks);

            foreach (var pageFics in pageResults)
            {
                results.AddRange(pageFics);
                if (results.Count >= 10) break; // stop once we have 10 works
            }

            return results.Take(10);
        }

        private async Task<IEnumerable<FicInfo>> FetchPageAsync(string baseUrl, int page)
        {
            for (int attempt = 1; attempt <= _ao3Config.MaxRetries; attempt++)
            {
                var url = $"{baseUrl}&page={page}";
                var html = await SafeGetStringAo3Async(url);

                if (!string.IsNullOrEmpty(html))
                    return ParseFicsFromHtml(html);

                if (attempt < _ao3Config.MaxRetries)
                {
                    var backoff = TimeSpan.FromMilliseconds(_ao3Config.MaxRetries * Math.Pow(2, attempt - 1));
                    _logger.LogWarning("[Ao3] Retry {Attempt}/{Max} for page {Page} after {Delay}ms", attempt, _ao3Config.MaxRetries, page, backoff.TotalMilliseconds);
                    await Task.Delay(backoff);
                }
            }

            _logger.LogError("[Ao3] Failed to fetch page {Page} after {Max} attempts", page, _ao3Config.MaxRetries);
            return [];
        }

        private async Task<string?> SafeGetStringAo3Async(string url)
        {
            await _Ao3Lock.WaitAsync();
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
                    _logger.LogInformation("[Ao3] Waiting {Delay}ms per robots policy", delay);
                    await Task.Delay(delay);
                }

                lock (_lastRequestLock)
                {
                    _lastAo3RequestUtc = DateTime.UtcNow;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("[Ao3-HTTP] GET {Url}", url);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    if ((int)resp.StatusCode == 429)
                    {
                        _logger.LogWarning("[Ao3] 429 Too Many Requests — backing off {Ms}ms", _ao3Config.Ao3BackoffMs);
                        await Task.Delay(_ao3Config.Ao3BackoffMs);
                        return null;
                    }

                    _logger.LogInformation("[Ao3-HTTP] Received {Status} after {Elapsed}ms",
                        resp.StatusCode, sw.ElapsedMilliseconds);

                    var text = await resp.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogInformation("[Ao3-HTTP] Body read {Bytes} bytes in {Elapsed}ms",
                        text?.Length ?? 0, sw.ElapsedMilliseconds);

                    return text;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("[Ao3] Timeout — backing off {Ms}ms", _ao3Config.Ao3BackoffMs);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Ao3] Request failed — backing off {Ms}ms", _ao3Config.Ao3BackoffMs);
                    await Task.Delay(_ao3Config.Ao3BackoffMs);
                    return null;
                }
            }
            finally
            {
                _Ao3Lock.Release();
            }
        }

        private static List<FicInfo> ParseFicsFromHtml(string? html)
        {
            if (html is null) return [];

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'work')]|//li[contains(@class,'work blurb group')]");
            if (nodes == null) return [];

            var results = new List<FicInfo>();
            foreach (var n in nodes)
            {
                var titleNode = n.SelectSingleNode(".//h4/a") ?? n.SelectSingleNode(".//h4/span/a");
                if (titleNode == null) continue;

                var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                var href = titleNode.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href)) continue;

                var full = href.StartsWith("http") ? href : "https://archiveofourown.org" + href;
                results.Add(new()
                {
                    Title = title,
                    Url = full
                });
            }

            return results;
        }
    }
}