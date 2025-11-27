using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Databases.DbModels
{
    public class CacheEntry
    {
        public CacheEntry() { }

        public CacheEntry(string key, string json, DateTime expiresAt)
        {
            Key = key;
            Json = json;
            ExpiresAt = expiresAt;
        }

        [Key]
        public required string Key { get; set; } = string.Empty;

        public required string Json { get; set; } = string.Empty;

        public required DateTime ExpiresAt { get; set; }
    }
}
