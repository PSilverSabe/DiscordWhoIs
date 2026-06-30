using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

public sealed class EmbedPosterConfigurationRepository(
    IDbContextFactory<BotDbContext> dbContextFactory,
    ILogger<EmbedPosterConfigurationRepository> logger)
    : RepositoryBase<BotDbContext, EmbedPosterConfigurationRepository>(dbContextFactory, logger),
      IEmbedPosterConfigurationRepository
{

    public async Task<IReadOnlyList<EmbedPosterConfiguration>> GetAllAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.EmbedPosterConfiguration.ToListAsync();
    }

    public async Task<EmbedPosterConfiguration?> GetByServerIdAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);
    }

    public async Task<EmbedPosterConfiguration?> GetByServerAndChannelAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // First try to get channel-specific configuration
        EmbedPosterConfiguration? channelConfig = await context.EmbedPosterConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId, cancellationToken);

        if (channelConfig is not null)
        {
            return channelConfig;
        }

        // Fall back to server configuration
        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);
    }

    public async Task<IEnumerable<EmbedPosterConfiguration>> GetAllByServerIdAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .Where(c => c.ServerId == serverId)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmbedPosterConfiguration> GetOrCreateServerConfigAsync(ulong serverId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration? existingConfig = await context.EmbedPosterConfiguration
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);

        if (existingConfig is not null)
        {
            return existingConfig;
        }

        var newConfig = new EmbedPosterConfiguration
        {
            ServerId = serverId,
            ChannelId = null,
            Enabled = false,
            DeduplicationWindowMinutes = 10
        };

        context.EmbedPosterConfiguration.Add(newConfig);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new EmbedPosterConfiguration for server {ServerId}", serverId);
        return newConfig;
    }

    public async Task<EmbedPosterConfiguration> GetOrCreateChannelConfigAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration? existingConfig = await context.EmbedPosterConfiguration
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId, cancellationToken);

        if (existingConfig is not null)
        {
            return existingConfig;
        }

        var newConfig = new EmbedPosterConfiguration
        {
            ServerId = serverId,
            ChannelId = channelId,
            Enabled = false,
            DeduplicationWindowMinutes = 10
        };

        context.EmbedPosterConfiguration.Add(newConfig);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new EmbedPosterConfiguration for server {ServerId} channel {ChannelId}", serverId, channelId);
        return newConfig;
    }

    public async Task UpdateServerEnabledAsync(ulong serverId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);

        config.Enabled = enabled;
        config.Server = null; // Detach navigation property to avoid tracking issues
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated EmbedPosterConfiguration enabled for server {ServerId} to {Enabled}", serverId, enabled);
    }

    public async Task UpdateServerChannelAsync(ulong serverId, ulong? channelId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);

        config.ChannelId = channelId;
        config.Server = null;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated EmbedPosterConfiguration channel for server {ServerId} to {ChannelId}", serverId, channelId?.ToString() ?? "any");
    }

    public async Task UpdateDeduplicationWindowAsync(ulong serverId, int minutes, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.ServerId == serverId && c.ChannelId == null, cancellationToken);

        config.DeduplicationWindowMinutes = minutes;
        config.Server = null;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated EmbedPosterConfiguration deduplication window for server {ServerId} to {Minutes}m", serverId, minutes);
    }

    public async Task UpdateChannelEnabledAsync(ulong serverId, ulong channelId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.ServerId == serverId && c.ChannelId == channelId, cancellationToken);

        config.Enabled = enabled;
        config.Server = null;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated EmbedPosterConfiguration enabled for server {ServerId} channel {ChannelId} to {Enabled}", serverId, channelId, enabled);
    }

    public async Task<bool> DeleteChannelConfigAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmbedPosterConfiguration? config = await context.EmbedPosterConfiguration
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId, cancellationToken);

        if (config is null)
        {
            return false;
        }

        context.EmbedPosterConfiguration.Remove(config);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted EmbedPosterConfiguration for server {ServerId} channel {ChannelId}", serverId, channelId);
        return true;
    }

    public async Task<bool> IsEnabledAsync(ulong serverId, ulong? channelId = null, CancellationToken cancellationToken = default)
    {
        EmbedPosterConfiguration? config = channelId.HasValue
            ? await GetByServerAndChannelAsync(serverId, channelId.Value, cancellationToken)
            : await GetByServerIdAsync(serverId, cancellationToken);

        return config?.Enabled ?? false;
    }
}
