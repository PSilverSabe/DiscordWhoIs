using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public class EmbedPosterEnableCommand(
    IEmbedPosterConfigurationRepository configRepository,
    ILogger<EmbedPosterEnableCommand> logger)
    : EmbedPosterCommandGroup
{
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly ILogger<EmbedPosterEnableCommand> _logger = logger;

    [SlashCommand("enable", "Enable or disable the AO3 embed poster")]
    public async Task SetEnabledAsync(
        [Summary("Enabled", "Whether the embed poster should be active")]
        bool enabled)
    {
        var statusLines = new List<string>();
        await DeferAsync(ephemeral: true);

        await _configRepository.SetEnabledAsync(enabled);

        await InteractionResponseHelper.UpdateOriginalResponseAsync(
            Context.Interaction, statusLines,
            enabled ? "✅ Embed poster **enabled**." : "⛔ Embed poster **disabled**.",
            _logger);
    }
}
