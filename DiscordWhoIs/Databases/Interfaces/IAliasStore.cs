using DiscordWhoIs.Databases.Models;

namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IAliasStore
    {
        IReadOnlyList<AliasEntry> GetAllAliases();
        bool TryResolve(string alias, out string real);
        bool TryGet(string alias, out AliasEntry? entry);
        Task AddOrUpdateAsync(string alias, string real, string? description = null);
        Task<bool> RemoveAsync(string alias);
    }
}
