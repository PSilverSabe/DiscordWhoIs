namespace DiscordWhoIs.Services
{
    using DiscordWhoIs.Interfaces;
    using HtmlAgilityPack;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class Ao3FicFeedService
    {
        private readonly HttpClient _http;
        private readonly IPersistentCache _cache;
        private readonly ILogger<Ao3FicFeedService> _logger;
        private readonly IAliasStore _aliasStore;
        private readonly long? _targetFandomId;
        private readonly TimeSpan _cacheLength;

        // ============================================
        // AO3 POLICY: Serialized requests
        // ============================================
        private static readonly SemaphoreSlim _ao3Lock = new(1, 1);
        private static DateTime _lastAo3RequestUtc = DateTime.MinValue;

        private const int Ao3MinimumDelayMs = 12000; // 12 seconds between requests
        private const int Ao3BackoffMs = 20000;      // Backoff on throttle/timeouts

        private static readonly Regex PageCountRegex = new(@"page=([0-9]+)", RegexOptions.Compiled);

        public Ao3FicFeedService(
            IHttpClientFactory httpClientFactory,
            IPersistentCache cache,
            ILogger<Ao3FicFeedService> logger,
            IConfiguration configuration,
            IAliasStore aliasStore)
        {
            _http = httpClientFactory.CreateClient("Ao3");
            _cache = cache;
            _logger = logger;
            _aliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));

            var successParse = long.TryParse(configuration?["Fandom:TargetFandom"]?.Trim(), out var retVal);

            if (_targetFandomId != null || !successParse)
            {
                _logger.LogInformation("Configured target fandom path: {Fandom}", _targetFandomId);
            }

            _targetFandomId = retVal;

            // =======================================================
            // AO3 POLICY: Honest, contactable User-Agent
            // =======================================================
            _http.DefaultRequestHeaders.UserAgent.Clear();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DiscordWhoIsBot/1.0 (+31625469+PSilverSabe@users.noreply.github.com)"
            );

            _http.Timeout = TimeSpan.FromSeconds(30);

            if (int.TryParse(configuration?["Cache:ExpirationInHours"], out var cl) && cl > 0)
                _cacheLength = TimeSpan.FromHours(cl);
            else
                _cacheLength = TimeSpan.FromHours(12);
        }

        public long? TargetFandomId => _targetFandomId;

        public string ResolveAo3Username(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var trimmed = input.Trim();
            if (_aliasStore.TryResolve(trimmed, out var real)) return real;
            return trimmed;
        }

        public (string Resolved, string? Description) ResolveAo3UsernameWithDescription(string input)
        {
            var resolved = ResolveAo3Username(input);
            string? desc = null;

            if (!string.IsNullOrWhiteSpace(input) && _aliasStore.TryGet(input.Trim(), out var byAlias))
            {
                desc = byAlias?.Description;
            }

            if (desc == null)
            {
                var match = _aliasStore.GetAllAliases().FirstOrDefault(e => string.Equals(e.Real, resolved, StringComparison.OrdinalIgnoreCase));
                if (match is not null) desc = match.Description;
            }

            return (resolved, desc);
        }

        public async Task<IEnumerable<(string Title, string Url)>> GetUserFicsAsync(string user)
        {
            var resolvedUser = ResolveAo3Username(user);

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("Resolved alias {Alias} -> {RealUser}", user, resolvedUser);

            var cacheKeyResolved = $"ao3_{resolvedUser}";
            if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyResolved, out var cached))
            {
                _logger.LogInformation("Persistent cache hit for {User}", resolvedUser);
                return cached!;
            }

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
            {
                var cacheKeyAlias = $"ao3_{user}";
                if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyAlias, out var aliasCached))
                {
                    _logger.LogInformation("Persistent cache hit for alias {Alias} (mapped to {Resolved})", user, resolvedUser);
                    _cache.Set(cacheKeyResolved, aliasCached!, _cacheLength);
                    return aliasCached!;
                }
            }

            _logger.LogInformation("Persistent cache miss for {User} - scraping AO3", resolvedUser);
            var results = await ScrapeAllPagesAsync(resolvedUser);

            _cache.Set(cacheKeyResolved, results, _cacheLength);
            return results;
        }

        private async Task<IEnumerable<(string Title, string Url)>> ScrapeAllPagesAsync(string user)
        {
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={TargetFandomId}";

            // First page
            var firstPageHtml = await SafeGetStringAo3Async(baseUrl);
            if (firstPageHtml is null) return Enumerable.Empty<(string, string)>();

            var results = new List<(string, string)>(ParseFicsFromHtml(firstPageHtml));

            // If we already have 10 or more, no need to fetch more pages
            if (results.Count >= 10)
                return results.Take(10);

            var doc = new HtmlDocument();
            doc.LoadHtml(firstPageHtml);

            // Discover number of pages
            var totalPages = 1;
            var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
            if (pagination != null)
            {
                var last = pagination.SelectNodes(".//li/a")
                    ?.Select(a => a.GetAttributeValue("href", ""))
                    .LastOrDefault(h => h.Contains("page="));

                if (last != null)
                {
                    var m = PageCountRegex.Match(last);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var p))
                        totalPages = p;
                }
            }

            _logger.LogInformation("User {User} has {Pages} pages", user, totalPages);

            // Fetch additional pages only if needed
            for (int page = 2; page <= totalPages; page++)
            {
                if (results.Count >= 10)
                    break; // Stop if we already have 10 or more

                var url = baseUrl + $"?page={page}";
                var pageHtml = await SafeGetStringAo3Async(url);
                results.AddRange(ParseFicsFromHtml(pageHtml));
            }

            // Return at most 10 results
            return results.Take(10);
        }

        private IEnumerable<(string, string)> ParseFicsFromHtml(string? html)
        {
            if (html is null) return Enumerable.Empty<(string, string)>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'work')]|//li[contains(@class,'work blurb group')]");
            if (nodes == null) return Enumerable.Empty<(string, string)>();

            var results = new List<(string, string)>();
            foreach (var n in nodes)
            {
                var titleNode = n.SelectSingleNode(".//h4/a") ?? n.SelectSingleNode(".//h4/span/a");
                if (titleNode == null) continue;

                var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                var href = titleNode.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href)) continue;

                var full = href.StartsWith("http") ? href : "https://archiveofourown.org" + href;
                results.Add((title, full));
            }

            return results;
        }

        private async Task<string?> SafeGetStringAo3Async(string url)
        {
            await _ao3Lock.WaitAsync();
            try
            {
                var elapsed = DateTime.UtcNow - _lastAo3RequestUtc;
                var delay = Math.Max(0, Ao3MinimumDelayMs - (int)elapsed.TotalMilliseconds);
                if (delay > 0)
                {
                    _logger.LogInformation("[AO3] Waiting {Delay}ms per robots policy", delay);
                    await Task.Delay(delay);
                }

                _lastAo3RequestUtc = DateTime.UtcNow;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("[AO3-HTTP] GET {Url}", url);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    if ((int)resp.StatusCode == 429)
                    {
                        _logger.LogWarning("[AO3] 429 Too Many Requests — backing off {Ms}ms", Ao3BackoffMs);
                        await Task.Delay(Ao3BackoffMs);
                        return null;
                    }

                    _logger.LogInformation("[AO3-HTTP] Received {Status} after {Elapsed}ms",
                        resp.StatusCode, sw.ElapsedMilliseconds);

                    var text = await resp.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogInformation("[AO3-HTTP] Body read {Bytes} bytes in {Elapsed}ms",
                        text?.Length ?? 0, sw.ElapsedMilliseconds);

                    return text;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("[AO3] Timeout — backing off {Ms}ms", Ao3BackoffMs);
                    await Task.Delay(Ao3BackoffMs);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AO3] Request failed — backing off {Ms}ms", Ao3BackoffMs);
                    await Task.Delay(Ao3BackoffMs);
                    return null;
                }
            }
            finally
            {
                _ao3Lock.Release();
            }
        }
    }
}
