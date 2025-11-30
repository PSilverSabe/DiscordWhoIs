namespace DiscordWhoIs.HumanFakers
{
    using Microsoft.Playwright;
    using System;
    using System.Threading.Tasks;

    public static class FakeHuman
    {
        private static readonly Random _rand = new();

        public static async Task PretendAsync(IPage page)
        {
            // 1. Random micro delays
            await Task.Delay(_rand.Next(80, 300));

            // 2. Random mouse movement
            for (int i = 0; i < _rand.Next(2, 6); i++)
            {
                var x = _rand.Next(0, 400);
                var y = _rand.Next(0, 400);
                await page.Mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = _rand.Next(3, 12) });
                await Task.Delay(_rand.Next(30, 120));
            }

            // 3. Random scroll
            if (_rand.NextDouble() < 0.7)
            {
                int scrollAmount = _rand.Next(150, 600);
                await page.EvaluateAsync($"window.scrollBy(0, {scrollAmount});");
                await Task.Delay(_rand.Next(80, 250));
            }

            // 4. Occasional viewport resize
            if (_rand.NextDouble() < 0.1)
            {
                int w = _rand.Next(900, 1400);
                int h = _rand.Next(700, 1100);
                await page.SetViewportSizeAsync(w, h);
            }

            // 5. Occasional keyboard noise
            if (_rand.NextDouble() < 0.05)
            {
                await page.Keyboard.PressAsync("Shift");
                await Task.Delay(_rand.Next(30, 120));
            }
        }
    }

}
