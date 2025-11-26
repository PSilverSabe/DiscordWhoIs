namespace DiscordWhoIs.Databases.Interfaces
{
    using System;

    public interface IPersistentCache
    {
        bool TryGetValue<T>(string key, out T? value);
        void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null);
        void Remove(string key);
    }
}