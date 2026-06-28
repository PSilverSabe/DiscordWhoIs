using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("enable", "Enable or disable the AO3 embed poster")]
    public async Task SetEnabledAsync(
        [Summary("Enabled", "Whether the embed poster should be active")]
        bool enabled)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            await _configRepository.SetEnabledAsync(enabled);

            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                enabled ? "✅ Embed poster **enabled**." : "⛔ Embed poster **disabled**.",
                _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster enable command.");
            throw;
        }
    }
}
