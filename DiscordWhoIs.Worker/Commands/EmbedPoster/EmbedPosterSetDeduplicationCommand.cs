using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("set-deduplication-window", "Set how long to suppress duplicate embeds for the same link")]
    public async Task SetDeduplicationWindowAsync(
        [Summary("Minutes", "Number of minutes to suppress duplicate embeds (1–1440)")]
        int minutes)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();

            if (minutes < 1 || minutes > 1440)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "Minutes must be between 1 and 1440 (24 hours).",
                    _logger);
                return;
            }

            await _configRepository.UpdateDeduplicationWindowAsync(serverId, minutes);
            _fanficEmbedResponderService.InvalidateServerConfigCache(serverId);

            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                $"✅ Deduplication window set to **{minutes} minute(s)**.",
                _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster set-deduplication-window command.");
            throw;
        }
    }
}
