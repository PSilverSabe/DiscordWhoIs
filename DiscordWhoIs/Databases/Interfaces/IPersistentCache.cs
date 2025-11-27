namespace DiscordWhoIs.Databases.Interfaces
{
    using DiscordWhoIs.Databases.DataModels;
    using System;

    public interface IPersistentCache
    {
        bool TryGetValue(string key, out IEnumerable<FicInfo> value);
        void SetAsync(string key, IEnumerable<FicInfo> value, TimeSpan absoluteExpirationRelativeToNow);
        Task RemoveAsync(string key);
    }
}