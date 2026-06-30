using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IServerRepository : IRepository<Server>
{
    /// <summary>
    /// Gets a server by its Discord ID.
    /// </summary>
    Task<Server?> GetByIdAsync(ulong serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a server by the internal database ID.
    /// </summary>
    Task<Server?> GetByDatabaseIdAsync(int databaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a server for the given Discord ID.
    /// </summary>
    Task<Server> GetOrCreateServerAsync(ulong serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new server.
    /// </summary>
    Task<Server> AddAsync(Server server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing server.
    /// </summary>
    Task<Server> UpdateAsync(Server server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a server by Discord ID.
    /// </summary>
    Task<bool> DeleteAsync(ulong serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a server exists by Discord ID.
    /// </summary>
    Task<bool> ExistsAsync(ulong serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a server by Discord ID, alias for GetByIdAsync.
    /// </summary>
    Task<Server?> GetServerByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);
}
