using DiscordWhoIs.Databases.DbModels;

namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IAliasRepository: IRepository<Alias>
    {
        Task<bool> TryResolveAsync(string alias, out string real);

        Task<bool> TryGetAsync(string alias, out Alias? entry);

        Task AddOrUpdateAsync(string alias, string real);

        Task<bool> RemoveAsync(string alias);
    }
}
