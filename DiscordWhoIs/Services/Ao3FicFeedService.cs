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

        public Ao3FicFeedService(
            HttpClient http,
            IPersistentCache cache,
            ILogger<Ao3FicFeedService> logger,
            IConfiguration configuration,
            IAliasStore aliasStore)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
            _aliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));

            // Read Fandom:TargetFandom from configuration (appsettings.json)
            var successParse = long.TryParse(configuration?["Fandom:TargetFandom"]?.Trim(), out var retVal);

            if (_targetFandomId != null || !successParse)
            {
                _logger.LogInformation("Configured target fandom path: {Fandom}", _targetFandomId);
            }

            _targetFandomId = retVal;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Ao3FicFeedBot/1.0 (+https://example.com)");

            if (int.TryParse(configuration?["Cache:ExpirationInHours"], out var cl) && cl > 0) _cacheLength = TimeSpan.FromHours(cl);
            else _cacheLength = TimeSpan.FromHours(12);
        }

        private static readonly Regex PageCountRegex = new(@"page=([0-9]+)", RegexOptions.Compiled);

        // Expose the configured fandom path for callers that may need it
        public long? TargetFandomId => _targetFandomId;

        // Resolve an input (alias or actual username) to the real AO3 account name.
        public string ResolveAo3Username(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var trimmed = input.Trim();
            if (_aliasStore.TryResolve(trimmed, out var real)) return real;
            return trimmed;
        }

        // Resolve username and return any configured description (from alias entry or from an entry whose real matches)
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

        // Public method that returns (Title, Url) tuples. Uses persistent caching to avoid repeated scraping.
        public async Task<IEnumerable<(string Title, string Url)>> GetUserFicsAsync(string user)
        {
            var resolvedUser = ResolveAo3Username(user);

            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Resolved alias {Alias} -> {RealUser}", user, resolvedUser);
            }

            var cacheKeyResolved = $"ao3_{resolvedUser}";
            if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyResolved, out var cached))
            {
                _logger.LogInformation("Persistent cache hit for {User}", resolvedUser);
                return cached!;
            }

            // Fallback: if user was an alias, check the alias key in cache to avoid re-scraping if we previously cached under the alias.
            if (!string.Equals(user, resolvedUser, StringComparison.OrdinalIgnoreCase))
            {
                var cacheKeyAlias = $"ao3_{user}";
                if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyAlias, out var aliasCached))
                {
                    _logger.LogInformation("Persistent cache hit for alias {Alias} (mapped to {Resolved})", user, resolvedUser);
                    // Re-save under resolved key for consistency
                    _cache.Set(cacheKeyResolved, aliasCached!, _cacheLength);
                    return aliasCached!;
                }
            }

            _logger.LogInformation("Persistent cache miss for {User} - scraping AO3", resolvedUser);
            var results = await ScrapeAllPagesAsync(resolvedUser);

            // Persist for 24 hours by default
            _cache.Set(cacheKeyResolved, results, _cacheLength);

            return results;
        }

        // Scrapes first page to discover total pages, then fetches pages concurrently
        private async Task<IEnumerable<(string Title, string Url)>> ScrapeAllPagesAsync(string user)
        {
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={TargetFandomId}";
            var firstPageHtml = await SafeGetStringAsync(baseUrl);
            if (firstPageHtml is null) return Enumerable.Empty<(string, string)>();

            var doc = new HtmlDocument();
            doc.LoadHtml(firstPageHtml);

            var totalPages = 1;
            // Try to find pagination links to determine number of pages
            var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
            if (pagination != null)
            {
                var last = pagination.SelectNodes(".//li/a")?.Select(a => a.GetAttributeValue("href", "")).LastOrDefault(h => h.Contains("page="));
                if (last != null)
                {
                    var m = PageCountRegex.Match(last);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var p)) totalPages = p;
                }
            }

            _logger.LogInformation("User {User} has {Pages} pages", user, totalPages);

            var pageUrls = Enumerable.Range(1, totalPages)
                .Select(p => p == 1 ? baseUrl : baseUrl + $"?page={p}")
                .ToArray();

            // Limit concurrency to avoid hammering AO3
            var semaphore = new SemaphoreSlim(5);
            var tasks = pageUrls.Select(async url =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var html = await SafeGetStringAsync(url);
                    return ParseFicsFromHtml(html);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var pages = await Task.WhenAll(tasks);
            return pages.SelectMany(x => x).ToList();
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
                // Title node
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

        private async Task<string?> SafeGetStringAsync(string url)
        {
            try
            {
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AO3 returned {Status} for {Url}", resp.StatusCode, url);
                    return null;
                }
                return await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {Url}", url);
                return null;
            }
        }
    }
}