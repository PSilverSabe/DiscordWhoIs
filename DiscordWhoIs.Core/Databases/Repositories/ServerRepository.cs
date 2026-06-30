using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

/// <summary>
/// Repository implementation for Server entity operations.
/// </summary>
public class ServerRepository(
    IDbContextFactory<BotDbContext> dbContextFactory,
    ILogger<ServerRepository> logger)
    : RepositoryBase<BotDbContext, ServerRepository>(dbContextFactory, logger), IServerRepository
{
    public async Task<Server?> GetByIdAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching server by Discord ID {DiscordServerId}", serverId);

        return await context.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DiscordServerId == serverId, cancellationToken);
    }

    public async Task<Server?> GetByDatabaseIdAsync(int databaseId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching server by database ID {DatabaseId}", databaseId);

        return await context.Servers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == databaseId, cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetAllAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching all servers");

        return await context.Servers
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Server> GetOrCreateServerAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Getting or creating server with Discord ID {DiscordServerId}", serverId);

        Server? server = await context.Servers
            .FirstOrDefaultAsync(s => s.DiscordServerId == serverId, cancellationToken);

        if (server is not null)
        {
            _logger.LogDebug("Server {DiscordServerId} already exists", serverId);
            return server;
        }

        _logger.LogInformation("Creating new server record for Discord ID {DiscordServerId}", serverId);

        var newServer = new Server
        {
            DiscordServerId = serverId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await context.Servers.AddAsync(newServer, cancellationToken);
        await SaveChangesAsync(context);

        return newServer;
    }

    public async Task<Server> AddAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Adding new server with Discord ID {DiscordServerId}", server.DiscordServerId);

        server.CreatedDate = DateTime.UtcNow;
        server.UpdatedDate = DateTime.UtcNow;

        await context.Servers.AddAsync(server, cancellationToken);
        await SaveChangesAsync(context);

        return server;
    }

    public async Task<Server> UpdateAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Updating server {DiscordServerId}", server.DiscordServerId);

        server.UpdatedDate = DateTime.UtcNow;

        context.Servers.Update(server);
        await SaveChangesAsync(context);

        return server;
    }

    public async Task<bool> DeleteAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Deleting server with Discord ID {DiscordServerId}", serverId);

        Server? server = await context.Servers
            .FirstOrDefaultAsync(s => s.DiscordServerId == serverId, cancellationToken);

        if (server is null)
        {
            _logger.LogWarning("Server {DiscordServerId} not found for deletion", serverId);
            return false;
        }

        context.Servers.Remove(server);
        await SaveChangesAsync(context);

        return true;
    }

    public async Task<bool> ExistsAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Checking if server {DiscordServerId} exists", serverId);

        return await context.Servers
            .AsNoTracking()
            .AnyAsync(s => s.DiscordServerId == serverId, cancellationToken);
    }

    public async Task<Server?> GetServerByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default) => await GetByIdAsync(discordId, cancellationToken);
}
