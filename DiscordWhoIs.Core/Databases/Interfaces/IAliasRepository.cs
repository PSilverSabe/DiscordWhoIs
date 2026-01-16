using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IAliasRepository : IRepository<Alias>
{
    Task AddOrUpdateAsync(string alias, string real);

    Task<bool> RemoveAsync(string alias);
}
