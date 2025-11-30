namespace DiscordWhoIs.Services
{
    using DiscordWhoIs.Configuration.Models;
    using DiscordWhoIs.Databases.DataModels;
    using DiscordWhoIs.Databases.Interfaces;
    using DiscordWhoIs.Models;
    using DiscordWhoIs.HumanFakers;
    using DiscordWhoIs.Regexs;
    using HtmlAgilityPack;
    using Microsoft.Extensions.Logging;
    using Microsoft.Playwright;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Ao3FicFeedService : IAsyncDisposable
    {
        private static readonly Lock _lastRequestLock = new();
        private static DateTime _lastAo3RequestUtc = DateTime.MinValue;

        private readonly IPersistentCache _cache;
        private readonly ILogger<Ao3FicFeedService> _logger;
        private readonly IAliasStore _aliasStore;
        private readonly SemaphoreSlim _Ao3Lock;

        private int _timeoutCount = 0;
        private DateTime? _lastTimeoutUtc = null;
        private string? _lastTimeoutMessage = null;
        private readonly Lock _timeoutLock = new();

        // Playwright
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;
        private static readonly string[] options = new[]
                {
                    "--start-maximized",
                    "--disable-blink-features=AutomationControlled" // reduce bot fingerprint
                };

        // Config Classes
        private readonly FandomConfiguration _fandomConfig;
        private readonly CacheConfiguration _cacheConfig;
        private readonly Ao3Configuration _ao3Config;
        private readonly ProxyConfiguration _proxyConfig;

        public Ao3FicFeedService(
            IPersistentCache cache,
            ILogger<Ao3FicFeedService> logger,
            IAliasStore aliasStore,
            FandomConfiguration fandomConfig,
            CacheConfiguration cacheConfig,
            Ao3Configuration ao3Config,
            ProxyConfiguration proxyConfig)
        {
            _cache = cache;
            _logger = logger;
            _aliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));
            _fandomConfig = fandomConfig;
            _cacheConfig = cacheConfig;
            _ao3Config = ao3Config;
            _proxyConfig = proxyConfig;

            _Ao3Lock = new SemaphoreSlim(_ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3ConcurrencyLimit);

            _logger.LogInformation("[ServiceInit] Ao3FicFeedService constructed. ConcurrencyLimit={Limit}, MinDelayMs={MinDelay}, BackoffMs={Backoff}, MaxRetries={MaxRetries}",
                _ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3MinimumDelayMs.TotalMilliseconds, _ao3Config.Ao3BackoffMs, _ao3Config.MaxRetries);

            _logger.LogInformation("[PlaywrightInit] Launching headless browser...");
            _playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            _browser = _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false, // Non-headless
                Args = options,
                Proxy = new Proxy
                {
                    Server = _proxyConfig.Address,
                    Username = _proxyConfig.Username,
                    Password = _proxyConfig.Password
                }
            }).GetAwaiter().GetResult();
            _logger.LogInformation("[PlaywrightInit] Headless browser launched successfully.");
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("[Dispose] Closing browser and Playwright...");
            if (_browser != null) await _browser.CloseAsync();
            _playwright?.Dispose();
            _logger.LogInformation("[Dispose] Browser closed.");
        }

        #region Public Methods

        public Ao3ThrottleStatus GetThrottleStatus()
        {
            DateTime last;
            lock (_lastRequestLock) last = _lastAo3RequestUtc;

            var now = DateTime.UtcNow;
            var since = now - last;
            var until = since < _ao3Config.Ao3MinimumDelayMs ? _ao3Config.Ao3MinimumDelayMs - since : TimeSpan.Zero;
            int availableSlots = _Ao3Lock.CurrentCount;

            _logger.LogTrace("[ThrottleStatus] SinceLastRequest={ElapsedMs}ms, UntilNextAllowed={UntilMs}ms, AvailableSlots={Slots}",
                since.TotalMilliseconds, until.TotalMilliseconds, availableSlots);

            return new Ao3ThrottleStatus(
                until > TimeSpan.Zero || availableSlots <= 0,
                since,
                until,
                availableSlots
            );
        }

        public Ao3TimeoutStatus GetTimeoutStatus()
        {
            lock (_timeoutLock)
            {
                bool degraded = _timeoutCount >= 3 &&
                                _lastTimeoutUtc.HasValue &&
                                (DateTime.UtcNow - _lastTimeoutUtc.Value) < TimeSpan.FromMinutes(10);

                _logger.LogTrace("[TimeoutStatus] TimeoutCount={Count}, LastTimeout={Last}, Degraded={Degraded}",
                    _timeoutCount, _lastTimeoutUtc, degraded);

                return new Ao3TimeoutStatus(_timeoutCount, _lastTimeoutUtc, _lastTimeoutMessage, degraded);
            }
        }

        public async Task<string> ResolveAo3UsernameAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var trimmed = input.Trim();
            var resolved = await Task.Run(() => _aliasStore.TryResolve(trimmed, out var real) ? real : trimmed);

            if (!string.Equals(trimmed, resolved, StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("[AliasResolved] {Alias} -> {Resolved}", trimmed, resolved);

            return resolved;
        }

        public async Task<Ao3ResponseStatus> GetUserFicsAsync(string user)
        {
            var resolvedUser = await ResolveAo3UsernameAsync(user);

            var cacheKey = resolvedUser;
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                _logger.LogInformation("[CacheHit] Returning cached fics for user '{User}' ({Count} fics)", resolvedUser, cached?.Count() ?? 0);
                return new Ao3ResponseStatus { IsSuccessful = true, Fics = cached! };
            }

            _logger.LogInformation("[CacheMiss] No cache for user '{User}', scraping AO3...", resolvedUser);
            var results = await ScrapeAllPagesConcurrentAsync(resolvedUser);

            if (results?.Fics.Any() ?? false)
            {
                _cache.SetAsync(cacheKey, results.Fics, _cacheConfig.ExpirationInHours);
                _logger.LogDebug("[CacheSet] Stored {Count} fics for user '{User}' in persistent cache", results.Fics.Count(), resolvedUser);
            }

            return results;
        }

        #endregion

        #region Scraping & Parsing

        private async Task<Ao3ResponseStatus> ScrapeAllPagesConcurrentAsync(string user)
        {
            var scrapeSw = Stopwatch.StartNew();
            _logger.LogInformation("[ScrapeStart] Begin scraping fics for user '{User}'", user);

            var responseStatus = new Ao3ResponseStatus();
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={_fandomConfig.TargetFandom}";
            _logger.LogDebug("[ScrapeInfo] Base URL: {Url}", baseUrl);

            var firstPageHtml = await FetchPageHeadlessAsync(baseUrl);
            if (firstPageHtml is null)
            {
                _logger.LogWarning("[ScrapeFail] First page returned null HTML for user '{User}'", user);
                responseStatus.IsSuccessful = false;
                responseStatus.Fics = Enumerable.Empty<FicInfo>();
                return responseStatus;
            }

            _logger.LogDebug("[ParseStart] Parsing first page HTML for user '{User}'", user);
            var parsedFirst = ParseFicsFromHtml(firstPageHtml);
            _logger.LogInformation("[ParseComplete] Parsed {Count} fics from first page for user '{User}'", parsedFirst.Count, user);

            var allFics = new List<FicInfo>(parsedFirst);

            if (allFics.Count == 0)
            {
                _logger.LogInformation("[ParseError] Parsed item resulted in zero fics, html as follows {html}", firstPageHtml);
            }

            int totalPages = 1;

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(firstPageHtml);
                var pagination = doc.DocumentNode.SelectSingleNode("//ol[contains(@class,'pagination')]");
                if (pagination != null)
                {
                    var lastPageHref = pagination.SelectNodes(".//li/a")
                        ?.Select(a => a.GetAttributeValue("href", ""))
                        .LastOrDefault(h => h.Contains("page="));

                    if (!string.IsNullOrEmpty(lastPageHref))
                    {
                        var match = Ao3Regex.PageCountRegex().Match(lastPageHref);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var p))
                            totalPages = p;
                        else
                            _logger.LogWarning("[ParseWarning] Failed to parse last page href '{Href}' for user '{User}'", lastPageHref, user);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseError] Exception determining total pages for user '{User}'", user);
            }

            _logger.LogInformation("[PaginationInfo] User '{User}' has {TotalPages} pages. Starting concurrent fetch...", user, totalPages);

            if (totalPages > 1)
            {
                var pageTasks = Enumerable.Range(2, totalPages - 1).Select(pageNum => Task.Run(async () =>
                {
                    _logger.LogDebug("[PageFetchStart] Fetching page {Page} for user '{User}'", pageNum, user);
                    var sw = Stopwatch.StartNew();
                    var html = await FetchPageHeadlessAsync($"{baseUrl}&page={pageNum}");
                    sw.Stop();

                    if (html != null)
                    {
                        _logger.LogDebug("[PageFetchComplete] Fetched page {Page} for user '{User}' in {ElapsedMs}ms", pageNum, user, sw.ElapsedMilliseconds);
                        var fics = ParseFicsFromHtml(html);
                        _logger.LogInformation("[PageParse] Page {Page} returned {Count} fics for user '{User}'", pageNum, fics.Count(), user);
                        return fics;
                    }
                    else
                    {
                        _logger.LogWarning("[PageFetchFail] Page {Page} returned null for user '{User}'", pageNum, user);
                        return Enumerable.Empty<FicInfo>();
                    }
                })).ToList();

                var pageResults = await Task.WhenAll(pageTasks);

                foreach (var fics in pageResults)
                {
                    allFics.AddRange(fics);
                    if (allFics.Count >= 10)
                    {
                        _logger.LogInformation("[EarlyStop] Collected {Count} fics (>=10) for user '{User}', stopping further page fetches", allFics.Count, user);
                        break;
                    }
                }
            }

            responseStatus.IsSuccessful = true;
            responseStatus.Fics = allFics.Take(10);
            scrapeSw.Stop();

            _logger.LogInformation("[ScrapeComplete] Completed scraping user '{User}'. Total collected: {Count}. Time elapsed: {ElapsedMs}ms",
                user, allFics.Count, scrapeSw.ElapsedMilliseconds);

            return responseStatus;
        }

        private async Task<string?> FetchPageHeadlessAsync(string url)
        {
            var waitSw = Stopwatch.StartNew();
            _logger.LogTrace("[SemaphoreWait] Waiting for semaphore for URL: {Url}", url);
            await _Ao3Lock.WaitAsync();
            waitSw.Stop();
            _logger.LogTrace("[SemaphoreEntered] Semaphore acquired for URL: {Url} after {ElapsedMs}ms", url, waitSw.ElapsedMilliseconds);

            try
            {
                int delay;
                lock (_lastRequestLock)
                {
                    var elapsed = DateTime.UtcNow - _lastAo3RequestUtc;
                    delay = Math.Max(0, (int)(_ao3Config.Ao3MinimumDelayMs.TotalMilliseconds - elapsed.TotalMilliseconds));
                }

                if (delay > 0)
                {
                    _logger.LogDebug("[Delay] Waiting {DelayMs}ms before fetching URL: {Url}", delay, url);
                    await Task.Delay(delay);
                }

                for (int attempt = 1; attempt <= _ao3Config.MaxRetries; attempt++)
                {
                    _logger.LogDebug("[FetchAttempt] Attempt {Attempt}/{MaxRetries} for URL: {Url}", attempt, _ao3Config.MaxRetries, url);
                    try
                    {
                        await using var context = await _browser.NewContextAsync(
                            new BrowserNewContextOptions
                            {
                                IgnoreHTTPSErrors = true,
                                BypassCSP = true,
                                ViewportSize = null, // full screen
                                JavaScriptEnabled = true, // enable JS
                                UserAgent = UserAgentProvider.GetRandomUserAgent(), // realistic UA
                                AcceptDownloads = false,
                                ExtraHTTPHeaders = new Dictionary<string, string>
                                {
                                    ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
                                    ["Accept-Language"] = "en-US,en;q=0.5"
                                },
                                // enable images, CSS by default
                            });
                        var page = await context.NewPageAsync();

                        // fake human interaction before navigation
                        await FakeHuman.PretendAsync(page);

                        await page.GotoAsync(url, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.Load,
                            Timeout = 20000
                        });

                        // after the page loads
                        await FakeHuman.PretendAsync(page);

                        // Random scroll to simulate reading
                        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight * Math.random());");
                        await Task.Delay(new Random().Next(160, 400));

                        // one more human pass before scraping
                        await FakeHuman.PretendAsync(page);

                        var content = await page.ContentAsync();

                        lock (_lastRequestLock) { _lastAo3RequestUtc = DateTime.UtcNow; }

                        if (string.IsNullOrWhiteSpace(content) || content.Length < 2000)
                        {
                            RecordTimeout(null, url);
                            _logger.LogWarning("[FetchWarning] Page content too small for URL: {Url}, retrying...", url);
                            await Task.Delay(_ao3Config.Ao3BackoffMs);
                            continue;
                        }

                        _logger.LogDebug("[FetchSuccess] Successfully fetched URL: {Url}, length: {Length}", url, content.Length);
                        return content;
                    }
                    catch (Exception ex)
                    {
                        RecordTimeout(ex, url);
                        _logger.LogError(ex, "[FetchError] Exception during attempt {Attempt}/{MaxRetries} for URL: {Url}, retrying after {BackoffMs}ms",
                            attempt, _ao3Config.MaxRetries, url, _ao3Config.Ao3BackoffMs.TotalMilliseconds);
                        await Task.Delay(_ao3Config.Ao3BackoffMs);
                    }
                }

                _logger.LogError("[FetchFail] Failed to fetch URL after {MaxAttempts} attempts: {Url}", _ao3Config.MaxRetries, url);
                return null;
            }
            finally
            {
                try
                {
                    _Ao3Lock.Release();
                }
                catch (SemaphoreFullException)
                {
                    _logger.LogWarning("[SemaphoreWarning] Semaphore already released for URL: {Url}", url);
                }
            }
        }

        private List<FicInfo> ParseFicsFromHtml(string? html)
        {
            if (html == null) return new List<FicInfo>();

            var doc = new HtmlDocument();
            try { doc.LoadHtml(html); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ParseError] Failed to load HTML");
                return new List<FicInfo>();
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
                    _logger.LogError(ex, "[ParseError] Exception parsing fic node");
                }
            }

            _logger.LogDebug("[ParseComplete] Parsed {Count} fics from HTML", results.Count);
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
                "[Ao3Timeout] Timeout recorded for URL: {Url}. Total: {Count}. Last message: {Msg}",
                url, _timeoutCount, _lastTimeoutMessage);
        }

        #endregion
    }
}
