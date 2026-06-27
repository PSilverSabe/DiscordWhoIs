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
    // Always reads/writes the single seeded row (Id = 1)
    private const int ConfigId = 1;

    public async Task<EmbedPosterConfiguration> GetAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        return await context.EmbedPosterConfiguration
                   .AsNoTracking()
                   .FirstAsync(c => c.Id == ConfigId);
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.Id == ConfigId);

        config.Enabled = enabled;
        await SaveChangesAsync(context);

        _logger.LogInformation("EmbedPoster enabled set to {Enabled}", enabled);
    }

    public async Task SetChannelAsync(ulong? channelId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.Id == ConfigId);

        config.ChannelId = channelId;
        await SaveChangesAsync(context);

        _logger.LogInformation("EmbedPoster channel set to {ChannelId}", channelId?.ToString() ?? "any");
    }

    public async Task SetDeduplicationWindowAsync(int minutes)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        EmbedPosterConfiguration config = await context.EmbedPosterConfiguration
            .FirstAsync(c => c.Id == ConfigId);

        config.DeduplicationWindowMinutes = minutes;
        await SaveChangesAsync(context);

        _logger.LogInformation("EmbedPoster deduplication window set to {Minutes}m", minutes);
    }
}
