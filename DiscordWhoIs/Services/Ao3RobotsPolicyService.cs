using DiscordWhoIs.Interfaces;

namespace DiscordWhoIs.Services
{
    public class Ao3RobotsPolicyService : IAo3RobotsPolicy
    {
        private readonly HashSet<string> _disallowed = new()
    {
        "/downloads/",
        "/admin/",
        "/api/",
        "/gifts/",
        "/search"
    };

        private DateTime _lastRequest = DateTime.MinValue;
        private readonly object _lock = new();

        public bool IsPathAllowed(string url)
        {
            var path = new Uri(url).AbsolutePath.ToLowerInvariant();

            foreach (var d in _disallowed)
                if (path.StartsWith(d))
                    return false;

            return true;
        }

        public Task EnforceRateLimitAsync()
        {
            lock (_lock)
            {
                var delta = DateTime.UtcNow - _lastRequest;
                if (delta.TotalMilliseconds < 1000)
                {
                    var delay = 1000 - (int)delta.TotalMilliseconds;
                    _lastRequest = DateTime.UtcNow.AddMilliseconds(delay);
                    return Task.Delay(delay);
                }

                _lastRequest = DateTime.UtcNow;
                return Task.CompletedTask;
            }
        }
    }
}
