using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;

namespace DiscordWhoIs.Core.Databases.Repositories;

public interface IServerRepository : IRepository<Server>
{
    Task<Server?> GetByIdAsync(int serverId, CancellationToken cancellationToken = default);

    Task<Server?> GetByNameAsync(string serverName, CancellationToken cancellationToken = default);

    Task<Server> AddAsync(Server server, CancellationToken cancellationToken = default);

    Task<Server> UpdateAsync(Server server, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int serverId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int serverId, CancellationToken cancellationToken = default);
}
