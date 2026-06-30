using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IEmbedPosterConfigurationRepository : IRepository<EmbedPosterConfiguration>
{
    Task<EmbedPosterConfiguration?> GetByServerIdAsync(ulong serverId, CancellationToken cancellationToken = default);

    Task<EmbedPosterConfiguration?> GetByServerAndChannelAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default);

    Task<IEnumerable<EmbedPosterConfiguration>> GetAllByServerIdAsync(ulong serverId, CancellationToken cancellationToken = default);

    Task<EmbedPosterConfiguration> GetOrCreateServerConfigAsync(ulong serverId, CancellationToken cancellationToken = default);

    Task<EmbedPosterConfiguration> GetOrCreateChannelConfigAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default);

    Task UpdateServerEnabledAsync(ulong serverId, bool enabled, CancellationToken cancellationToken = default);

    Task UpdateServerChannelAsync(ulong serverId, ulong? channelId, CancellationToken cancellationToken = default);

    Task UpdateDeduplicationWindowAsync(ulong serverId, int minutes, CancellationToken cancellationToken = default);

    Task UpdateChannelEnabledAsync(ulong serverId, ulong channelId, bool enabled, CancellationToken cancellationToken = default);

    Task<bool> DeleteChannelConfigAsync(ulong serverId, ulong channelId, CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(ulong serverId, ulong? channelId = null, CancellationToken cancellationToken = default);
}
