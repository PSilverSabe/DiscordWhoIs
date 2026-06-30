using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IEmbedPosterConfigurationRepository
{
    /// <summary>
    /// Gets all channel configurations for a specific server.
    /// </summary>
    Task<IReadOnlyList<EmbedPosterConfiguration>> GetByServerIdAsync(int serverId);

    /// <summary>
    /// Gets a specific channel configuration for a server.
    /// </summary>
    Task<EmbedPosterConfiguration?> GetByServerAndChannelAsync(int serverId, ulong channelId);

    /// <summary>
    /// Gets all enabled channels for a specific server.
    /// </summary>
    Task<IReadOnlyList<EmbedPosterConfiguration>> GetEnabledChannelsByServerIdAsync(int serverId);

    /// <summary>
    /// Creates or updates a channel configuration for a server.
    /// </summary>
    Task<bool> UpsertChannelConfigurationAsync(int serverId, ulong channelId, bool enabled, int deduplicationWindowMinutes = 10);

    /// <summary>
    /// Deletes a channel configuration for a server.
    /// </summary>
    Task<bool> DeleteChannelConfigurationAsync(int serverId, ulong channelId);

    /// <summary>
    /// Checks if a channel is enabled for embed posting in a server.
    /// </summary>
    Task<bool> IsChannelEnabledAsync(int serverId, ulong channelId);

    /// <summary>
    /// Gets all channel configurations across all servers.
    /// </summary>
    Task<IReadOnlyList<EmbedPosterConfiguration>> GetAllAsync();
}
