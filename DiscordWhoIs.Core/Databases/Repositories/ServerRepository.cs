using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
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

    public async Task<Server?> GetByIdAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Set<Server>().FirstOrDefaultAsync(s => s.ServerId == serverId, cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetAllAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Set<Server>().ToListAsync();
    }

    public async Task<Server?> GetByNameAsync(string serverName, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Set<Server>().FirstOrDefaultAsync(s => s.ServerName == serverName, cancellationToken);
    }

    public async Task<Server> AddAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        server.CreatedDate = DateTime.UtcNow;
        server.UpdatedDate = DateTime.UtcNow;

        await context.Set<Server>().AddAsync(server, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return server;
    }

    public async Task<Server> UpdateAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        server.UpdatedDate = DateTime.UtcNow;

        context.Set<Server>().Update(server);
        await context.SaveChangesAsync(cancellationToken);

        return server;
    }

    public async Task<bool> DeleteAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        Server? server = await GetByIdAsync(serverId, cancellationToken);
        if (server is null)
        {
            return false;
        }

        context.Set<Server>().Remove(server);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ExistsAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Set<Server>().AnyAsync(s => s.ServerId == serverId, cancellationToken);
    }
}
