using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

public class EmbedPosterConfigurationRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<EmbedPosterConfigurationRepository> logger)
    : RepositoryBase<BotDbContext, EmbedPosterConfigurationRepository>(dbContextFactory, logger), IEmbedPosterConfigurationRepository
{
    public async Task<IReadOnlyList<EmbedPosterConfiguration>> GetByServerIdAsync(int serverId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching all channel configurations for server {DiscordServerId}", serverId);

        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .Where(c => c.ServerId == serverId)
            .ToListAsync();
    }

    public async Task<EmbedPosterConfiguration?> GetByServerAndChannelAsync(int serverId, ulong channelId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching channel configuration for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);

        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId);
    }

    public async Task<IReadOnlyList<EmbedPosterConfiguration>> GetEnabledChannelsByServerIdAsync(int serverId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching enabled channel configurations for server {DiscordServerId}", serverId);

        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .Where(c => c.ServerId == serverId && c.Enabled)
            .ToListAsync();
    }

    public async Task<bool> UpsertChannelConfigurationAsync(int serverId, ulong channelId, bool enabled, int deduplicationWindowMinutes = 10)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Upserting channel configuration for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);

        EmbedPosterConfiguration? existing = await context.EmbedPosterConfiguration
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId);

        if (existing != null)
        {
            existing.Enabled = enabled;
            existing.DeduplicationWindowMinutes = deduplicationWindowMinutes;
            existing.UpdatedDate = DateTime.UtcNow;
            context.EmbedPosterConfiguration.Update(existing);
        }
        else
        {
            var newConfig = new EmbedPosterConfiguration
            {
                ServerId = serverId,
                ChannelId = channelId,
                Enabled = enabled,
                DeduplicationWindowMinutes = deduplicationWindowMinutes,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            context.EmbedPosterConfiguration.Add(newConfig);
        }

        try
        {
            await SaveChangesAsync(context);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting channel configuration for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);
            return false;
        }
    }

    public async Task<bool> DeleteChannelConfigurationAsync(int serverId, ulong channelId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Deleting channel configuration for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);

        EmbedPosterConfiguration? config = await context.EmbedPosterConfiguration
            .FirstOrDefaultAsync(c => c.ServerId == serverId && c.ChannelId == channelId);

        if (config == null)
        {
            _logger.LogWarning("Channel configuration not found for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);
            return false;
        }

        context.EmbedPosterConfiguration.Remove(config);

        try
        {
            await SaveChangesAsync(context);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting channel configuration for server {DiscordServerId} and channel {ChannelId}", serverId, channelId);
            return false;
        }
    }

    public async Task<bool> IsChannelEnabledAsync(int serverId, ulong channelId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Checking if channel {ChannelId} is enabled in server {DiscordServerId}", channelId, serverId);

        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .AnyAsync(c => c.ServerId == serverId && c.ChannelId == channelId && c.Enabled);
    }

    public async Task<IReadOnlyList<EmbedPosterConfiguration>> GetAllAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching all channel configurations");

        return await context.EmbedPosterConfiguration
            .AsNoTracking()
            .ToListAsync();
    }
}
