namespace DiscordWhoIs.Configuration.Models
{
    public class Ao3Configuration
    {
        public TimeSpan Ao3MinimumDelayMs { get; set; } = TimeSpan.FromSeconds(12);

        public TimeSpan Ao3BackoffMs { get; set; } = TimeSpan.FromSeconds(10);

        public int Ao3ConcurrencyLimit { get; set; } = 1;

        public int MaxRetries { get; set; } = 3;

        public TimeSpan RetryDelayMilliseconds { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}