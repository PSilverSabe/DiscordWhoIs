namespace DiscordWhoIs.Services
{
    using DiscordWhoIs.Configuration.Models;
    using DiscordWhoIs.Databases.DataModels;
    using DiscordWhoIs.Databases.Interfaces;
    using DiscordWhoIs.HumanFakers;
    using DiscordWhoIs.Models;
    using DiscordWhoIs.Regexs;
    using HtmlAgilityPack;
    using Microsoft.Extensions.Logging;
    using Microsoft.Playwright;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    public class Ao3FicFeedService : IAsyncDisposable
    {
        private static readonly Lock _lastRequestLock = new();
        private static DateTime _lastAo3RequestUtc = DateTime.MinValue;

        private readonly ILogger<Ao3FicFeedService> _logger;
        private readonly IAliasRepository _aliasRepository;
        private readonly IFanficRepository _fanficRepository;

        // Playwright (lazy-initialized)
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private static readonly string[] _options = new[]
                {
                    "--no-default-browser-check",
                    "--no-first-run",
                    "--disable-infobars",
                    "--password-store=basic",
                    "--use-mock-keychain",
                    "--start-maximized",
                    "--disable-blink-features=AutomationControlled" // reduce bot fingerprint
                };

        // Page pool (lazy-created)
        private Channel<IPage>? _pagePool;
        private readonly List<IPage> _createdPages = new();
        private readonly List<IBrowserContext> _createdContexts = new();
        private int _poolSize = 0;
        private int _inUseCount = 0;
        private readonly Lock _poolLock = new();

        // Init guard
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _initialized = false;

        // Config Classes
        private readonly FandomConfiguration _fandomConfig;
        private readonly CacheConfiguration _cacheConfig;
        private readonly Ao3Configuration _ao3Config;

        public Ao3FicFeedService(
            ILogger<Ao3FicFeedService> logger,
            IAliasRepository aliasRepository,
            IFanficRepository fanficRepository,
            FandomConfiguration fandomConfig,
            CacheConfiguration cacheConfig,
            Ao3Configuration ao3Config)
        {
            _logger = logger;
            _aliasRepository = aliasRepository ?? throw new ArgumentNullException(nameof(aliasRepository));
            _fanficRepository = fanficRepository ?? throw new ArgumentNullException(nameof(fanficRepository));
            _fandomConfig = fandomConfig;
            _cacheConfig = cacheConfig;
            _ao3Config = ao3Config;

            _logger.LogInformation("[ServiceInit] Ao3FicFeedService constructed. ConcurrencyLimit={Limit}, MinDelayMs={MinDelay}, BackoffMs={Backoff}, MaxRetries={MaxRetries}",
                _ao3Config.Ao3ConcurrencyLimit, _ao3Config.Ao3MinimumDelayMs.TotalMilliseconds, _ao3Config.Ao3BackoffMs, _ao3Config.MaxRetries);

            _logger.LogInformation("[PlaywrightInit] Playwright/browser will be initialized lazily on first scrape.");
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;

            await _initSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized) return;

                _logger.LogInformation("[PlaywrightInit] Launching browser (lazy init)...");

                _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Args = _options.ToArray()
                }).ConfigureAwait(false);

                // Page pool == concurrency limit
                _poolSize = Math.Max(1, _ao3Config.Ao3ConcurrencyLimit);
                _pagePool = Channel.CreateBounded<IPage>(new BoundedChannelOptions(_poolSize)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = false
                });

                // One persistent profile per context
                for (int i = 0; i < _poolSize; i++)
                {
                    var profileDir = Path.Combine("playwright_profiles", $"ao3_profile_{i}");
                    Directory.CreateDirectory(profileDir);

                    var storageStateFile = Path.Combine(profileDir, "storageState.json");
                    var identity = UserAgentProvider.GetRandomIdentity();

                    var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        UserAgent = identity.UserAgent,
                        Locale = identity.Locale,
                        TimezoneId = identity.Timezone,
                        ExtraHTTPHeaders = new Dictionary<string, string>
                        {
                            ["Accept-Language"] = identity.AcceptLanguage
                        },
                        ScreenSize = new ScreenSize { Width = 1366, Height = 768 },
                        DeviceScaleFactor = 1,
                    });

                    var page = await context.NewPageAsync().ConfigureAwait(false);

                    // Save storage state immediately so next boot has it
                    try
                    {
                        await context.StorageStateAsync(new() { Path = storageStateFile }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Init] Could not save initial storageState for {Profile}", profileDir);
                    }

                    _createdContexts.Add(context);
                    _createdPages.Add(page);

                    await _pagePool.Writer.WriteAsync(page).ConfigureAwait(false);
                }

                _initialized = true;
                _logger.LogInformation("[PlaywrightInit] Init complete. Pool size = {PoolSize}", _poolSize);
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("[Dispose] Saving storage states and closing resources...");

            // Save storage state for each context
            for (int i = 0; i < _createdContexts.Count; i++)
            {
                var ctx = _createdContexts[i];
                var profileDir = Path.Combine("playwright_profiles", $"ao3_profile_{i}");
                var storageFile = Path.Combine(profileDir, "storageState.json");

                try
                {
                    Directory.CreateDirectory(profileDir);
                    await ctx.StorageStateAsync(new() { Path = storageFile }).ConfigureAwait(false);
                    _logger.LogDebug("[Dispose] Saved storageState for profile {Profile}", profileDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Dispose] Failed to save storageState for context #{Index}", i);
                }
            }

            // Close pages
            foreach (var p in _createdPages)
            {
                try
                {
                    await p.CloseAsync().ConfigureAwait(false);
                }
                catch { }
            }
            _createdPages.Clear();

            // Close contexts
            foreach (var ctx in _createdContexts)
            {
                try
                {
                    await ctx.CloseAsync().ConfigureAwait(false);
                }
                catch { }
            }
            _createdContexts.Clear();

            // Close browser
            if (_browser != null)
                await _browser.CloseAsync().ConfigureAwait(false);

            _playwright?.Dispose();
            _logger.LogInformation("[Dispose] Shutdown complete.");
        }

        #region Public Methods

        public Ao3ThrottleStatus GetThrottleStatus()
        {
            DateTime last;
            lock (_lastRequestLock) last = _lastAo3RequestUtc;

            var now = DateTime.UtcNow;
            var since = now - last;
            var until = since < _ao3Config.Ao3MinimumDelayMs ? _ao3Config.Ao3MinimumDelayMs - since : TimeSpan.Zero;
            int availableSlots;
            lock (_poolLock)
            {
                availableSlots = _poolSize - _inUseCount;
                if (availableSlots < 0) availableSlots = 0;
            }

            _logger.LogTrace("[ThrottleStatus] SinceLastRequest={ElapsedMs}ms, UntilNextAllowed={UntilMs}ms, AvailableSlots={Slots}",
                since.TotalMilliseconds, until.TotalMilliseconds, availableSlots);

            return new Ao3ThrottleStatus(
                until > TimeSpan.Zero || availableSlots <= 0,
                since,
                until,
                availableSlots
            );
        }

        public async Task<string> ResolveAo3UsernameAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var trimmed = input.Trim();
            var resolved = await _aliasRepository.TryResolveAsync(trimmed, out var real);

            if (resolved)
            {
                _logger.LogInformation("[AliasResolved] {Alias} -> {Resolved}", trimmed, resolved);
                return real;
            }

            return trimmed;
        }

        public async Task<Ao3ResponseStatus> GetUserFicsAsync(string user)
        {
            var resolvedUser = await ResolveAo3UsernameAsync(user).ConfigureAwait(false);
            var anyFics = await _fanficRepository.GetAllByAuthorAsync(resolvedUser);

            if (anyFics.Count != 0)
            {
                _logger.LogInformation("[CacheHit] Returning cached fics for user '{User}' ({Count} fics)", resolvedUser, anyFics?.Count ?? 0);
                return new Ao3ResponseStatus 
                { 
                    IsSuccessful = true, 
                    Fics = anyFics?.Select(x => new FicInfo() 
                    {
                        Title = x.Title,
                        Url = x.Link
                    })!
                };
            }

            _logger.LogInformation("[CacheMiss] No cache for user '{User}', The user is either new or incorrect.", resolvedUser);

            return new Ao3ResponseStatus()
            {
                IsSuccessful = false,
                Fics = Enumerable.Empty<FicInfo>()
            };
        }

        #endregion

        #region Scraping & Parsing

        private async Task<Ao3ResponseStatus> ScrapeAllPagesConcurrentAsync(string user)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var scrapeSw = Stopwatch.StartNew();
            _logger.LogInformation("[ScrapeStart] Begin scraping fics for user '{User}'", user);

            var responseStatus = new Ao3ResponseStatus();
            var baseUrl = $"https://archiveofourown.org/users/{user}/works/?fandom_id={_fandomConfig.TargetFandom}";
            _logger.LogDebug("[ScrapeInfo] Base URL: {Url}", baseUrl);

            var firstPageHtml = await FetchPageHeadlessAsync(baseUrl).ConfigureAwait(false);
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
                var pageNums = Enumerable.Range(2, totalPages - 1).ToList();
                var tasks = new List<Task<IEnumerable<FicInfo>>>();

                // start tasks up to pool size (pool bounds concurrency)
                foreach (var pageNum in pageNums)
                {
                    tasks.Add(FetchAndParsePageAsync($"{baseUrl}&page={pageNum}", pageNum, user));
                }

                var pageResults = await Task.WhenAll(tasks).ConfigureAwait(false);

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

        private async Task<IEnumerable<FicInfo>> FetchAndParsePageAsync(string url, int pageNum, string user)
        {
            _logger.LogDebug("[PageFetchStart] Fetching page {Page} for user '{User}'", pageNum, user);
            var sw = Stopwatch.StartNew();
            var html = await FetchPageHeadlessAsync(url).ConfigureAwait(false);
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
        }

        private async Task<string?> FetchPageHeadlessAsync(string url)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            IPage? page = null;

            try
            {
                // AO3 throttle delay
                int delay;
                lock (_lastRequestLock)
                {
                    var elapsed = DateTime.UtcNow - _lastAo3RequestUtc;
                    delay = Math.Max(0, (int)(_ao3Config.Ao3MinimumDelayMs.TotalMilliseconds - elapsed.TotalMilliseconds));
                }

                if (delay > 0)
                {
                    _logger.LogDebug("[Delay] {Delay}ms before requesting {Url}", delay, url);
                    await Task.Delay(delay).ConfigureAwait(false);
                }

                page = await AcquirePageAsync().ConfigureAwait(false);

                for (int attempt = 1; attempt <= _ao3Config.MaxRetries; attempt++)
                {
                    try
                    {
                        // Slight human-like delay before navigation
                        await Task.Delay(Random.Shared.Next(60, 250)).ConfigureAwait(false);

                        await FakeHuman.PretendAsync(page).ConfigureAwait(false);

                        await page.GotoAsync(url, new()
                        {
                            WaitUntil = WaitUntilState.Load,
                            Timeout = 20000
                        }).ConfigureAwait(false);

                        await FakeHuman.PretendAsync(page).ConfigureAwait(false);

                        var content = await page.ContentAsync().ConfigureAwait(false);

                        lock (_lastRequestLock)
                        {
                            _lastAo3RequestUtc = DateTime.UtcNow;
                        }

                        if (string.IsNullOrWhiteSpace(content) || content.Length < 2000)
                        {
                            _logger.LogWarning("[Fetch] Content too small on attempt {A} for {Url}", attempt, url);
                            await Task.Delay(_ao3Config.Ao3BackoffMs).ConfigureAwait(false);
                            continue;
                        }

                        return content;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[FetchError] Attempt {A}/{Max} failed for {Url}", attempt, _ao3Config.MaxRetries, url);

                        await Task.Delay(_ao3Config.Ao3BackoffMs).ConfigureAwait(false);
                    }
                }

                _logger.LogError("[FetchFail] All retries failed for {Url}", url);
                return null;
            }
            finally
            {
                if (page != null)
                {
                    try
                    {
                        // Cleanup: reset page before returning to pool
                        if (page.Url != "about:blank")
                            await page.GotoAsync("about:blank", new() { Timeout = 5000 }).ConfigureAwait(false);
                    }
                    catch { /* ignore */ }

                    await ReleasePageAsync(page).ConfigureAwait(false);
                }
            }
        }

        private async Task<IPage> AcquirePageAsync()
        {
            var p = await _pagePool!.Reader.ReadAsync().ConfigureAwait(false);
            lock (_poolLock)
            {
                _inUseCount++;
            }

            return p;
        }

        private async Task ReleasePageAsync(IPage page)
        {
            // return page to pool (best-effort)
            try
            {
                await _pagePool!.Writer.WriteAsync(page).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PoolWarning] Failed to return page to pool; attempting to close.");
                try { await page.CloseAsync().ConfigureAwait(false); } catch { }
            }
            finally
            {
                lock (_poolLock)
                {
                    _inUseCount = Math.Max(0, _inUseCount - 1);
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
    }
}