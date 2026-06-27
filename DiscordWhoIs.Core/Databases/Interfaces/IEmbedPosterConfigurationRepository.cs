using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IEmbedPosterConfigurationRepository
{
    Task<EmbedPosterConfiguration> GetAsync();
    Task SetEnabledAsync(bool enabled);
    Task SetChannelAsync(ulong? channelId);
    Task SetDeduplicationWindowAsync(int minutes);
}
