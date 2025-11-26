using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.Models
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public sealed class CacheEntry
    {
        public CacheEntry() { }

        public CacheEntry(string key, string typeName, string json, DateTime? expiresAt = null)
        {
            Key = key;
            TypeName = typeName;
            Json = json;
            ExpiresAt = expiresAt;
        }

        [Key]
        public string Key { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public string Json { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }
    }
}
