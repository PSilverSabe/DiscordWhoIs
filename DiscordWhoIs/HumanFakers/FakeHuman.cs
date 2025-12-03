using Microsoft.Playwright;

namespace DiscordWhoIs.HumanFakers
{
    public static class FakeHuman
    {
        private static readonly Random _rnd = new();

        /// <summary>
        /// Perform light, randomized, non-destructive interactions to make page activity look human.
        /// Probabilities ensure behavior varies between requests.
        /// </summary>
        public static async Task PretendAsync(IPage page)
        {
            if (page == null) return;

            try
            {
                // small random delay before starting (human pause)
                await Task.Delay(_rnd.Next(50, 220)).ConfigureAwait(false);

                // 60% chance to perform small mouse jitter / moves
                if (Chance(0.6))
                {
                    await DoMouseJitterAsync(page).ConfigureAwait(false);
                }

                // 80% chance to perform a scroll based on page height (simulates reading)
                if (Chance(0.8))
                {
                    await DoScrollReadingAsync(page).ConfigureAwait(false);
                }

                // 30% chance to hover an anchor element
                if (Chance(0.3))
                {
                    await TryHoverAnchorAsync(page).ConfigureAwait(false);
                }

                // 10% chance to do tiny typing in a search field (non-destructive)
                if (Chance(0.10))
                {
                    await TryTinyTypingAsync(page).ConfigureAwait(false);
                }

                // final short idle
                await Task.Delay(_rnd.Next(120, 420)).ConfigureAwait(false);
            }
            catch
            {
                // Always swallow to avoid breaking scraping flow.
            }
        }

        private static bool Chance(double probability) => _rnd.NextDouble() < probability;

        private static async Task DoMouseJitterAsync(IPage page)
        {
            // Try to use page viewport if available; fallback to common desktop size
            int width = page.ViewportSize?.Width ?? 1366;
            int height = page.ViewportSize?.Height ?? 768;

            int moves = _rnd.Next(2, 7);
            for (int i = 0; i < moves; i++)
            {
                var x = _rnd.Next(50, Math.Max(100, width - 50));
                var y = _rnd.Next(50, Math.Max(100, height - 50));
                try
                {
                    await page.Mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = _rnd.Next(4, 14) }).ConfigureAwait(false);
                }
                catch { /* ignore transient */ }
                await Task.Delay(_rnd.Next(60, 240)).ConfigureAwait(false);
            }
        }

        private static async Task DoScrollReadingAsync(IPage page)
        {
            try
            {
                // Determine scroll height
                var scrollHeightObj = await page.EvaluateAsync<object>("() => document.body ? document.body.scrollHeight : 0").ConfigureAwait(false);
                int scrollHeight = 0;
                if (scrollHeightObj != null && int.TryParse(scrollHeightObj.ToString(), out var h)) scrollHeight = h;

                if (scrollHeight <= 0)
                {
                    // fallback: small random scroll
                    await page.EvaluateAsync("() => window.scrollBy(0, window.innerHeight * 0.25)").ConfigureAwait(false);
                    await Task.Delay(_rnd.Next(200, 700)).ConfigureAwait(false);
                    return;
                }

                // Simulate reading by scrolling 1-3 incremental chunks, with pauses proportional to chunk size
                int chunks = _rnd.Next(1, 4);
                for (int i = 0; i < chunks; i++)
                {
                    // scroll to a fraction of the page, with a bit of randomness
                    double frac = (i + 1) / (double)chunks;
                    frac = Math.Min(1.0, Math.Max(0.05, frac + (_rnd.NextDouble() - 0.5) * 0.15));
                    var pos = (int)(scrollHeight * frac);
                    await page.EvaluateAsync($"window.scrollTo({{ top: {pos}, behavior: 'smooth' }} )").ConfigureAwait(false);
                    // dwell time: longer for larger chunks
                    await Task.Delay(_rnd.Next(250, 900) + (int)(pos / 200.0)).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore errors, don't break scraping
            }
        }

        private static async Task TryHoverAnchorAsync(IPage page)
        {
            try
            {
                var anchor = await page.QuerySelectorAsync("a[href]").ConfigureAwait(false);
                if (anchor != null)
                {
                    await anchor.HoverAsync().ConfigureAwait(false);
                    await Task.Delay(_rnd.Next(120, 380)).ConfigureAwait(false);
                }
            }
            catch { /* ignore */ }
        }

        private static async Task TryTinyTypingAsync(IPage page)
        {
            try
            {
                var search = await page.QuerySelectorAsync("input[type='search'], input[name*='search'], input[placeholder*='Search']").ConfigureAwait(false);
                if (search != null)
                {
                    await search.FocusAsync().ConfigureAwait(false);
                    // typed text is intentionally tiny and harmless - a single character or two
                    string text = _rnd.NextDouble() < 0.5 ? "a" : "ok";
                    foreach (var ch in text)
                    {
                        await page.Keyboard.TypeAsync(ch.ToString()).ConfigureAwait(false);
                        await Task.Delay(_rnd.Next(90, 200)).ConfigureAwait(false);
                    }
                    await Task.Delay(_rnd.Next(200, 600)).ConfigureAwait(false);

                    // clear input to avoid leaving data behind
                    try
                    {
                        await page.Keyboard.PressAsync("Control+A").ConfigureAwait(false);
                        await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
                    }
                    catch { /* best-effort cleanup */ }
                }
            }
            catch { /* ignore */ }
        }
    }
}
