using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

[Group("embed-poster", "Configure the AO3 embed poster")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public partial class EmbedPosterCommand(
    IEmbedPosterConfigurationRepository configRepository,
    ILogger<EmbedPosterCommand> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly ILogger<EmbedPosterCommand> _logger = logger;
}
