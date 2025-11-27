using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Configuration.Models
{
    public class CacheConfiguration
    {
        [Required]
        public required string Path { get; set; }

        [Range(1, int.MaxValue)]
        public TimeSpan FlushIntervalSeconds { get; set; } = TimeSpan.FromSeconds(15);

        [Range(1, int.MaxValue)]
        public TimeSpan CleanupIntervalSeconds { get; set; } = TimeSpan.FromSeconds(45);

        [Range(1, int.MaxValue)]
        public TimeSpan ExpirationInHours { get; set; } = TimeSpan.FromHours(12);
    }
}
