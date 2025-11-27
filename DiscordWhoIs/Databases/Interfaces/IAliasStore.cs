using DiscordWhoIs.Databases.DbModels;

namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IAliasStore
    {
        IReadOnlyList<AliasEntry> GetAllAliases();
        bool TryResolve(string alias, out string real);
        bool TryGet(string alias, out AliasEntry? entry);
        Task AddOrUpdateAsync(string alias, string real);
        Task<bool> RemoveAsync(string alias);
    }
}
