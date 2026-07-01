using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("set-deduplication-window", "Set how long to suppress duplicate embeds for the same link")]
    public async Task SetDeduplicationWindowAsync(
        [Summary("Channel", "The channel to configure")]
        ITextChannel channel,
        [Summary("Minutes", "Deduplication window in minutes (1-120)")]
        [MinValue(1)]
        [MaxValue(120)]
        int deduplicationWindowMinutes)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();

            // Get or create server
            Server server = await _serverRepository.GetOrCreateServerAsync(serverId);

            // Update the channel's deduplication window
            bool success = await _channelConfigRepository.UpsertChannelConfigurationAsync(
                server.Id, channel.Id, enabled: true, deduplicationWindowMinutes);

            if (success)
            {
                _fanficEmbedResponderService.InvalidateChannelConfigCache(serverId, channel.Id);
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    $"✅ Deduplication window for {channel.Mention} set to **{deduplicationWindowMinutes} minute(s)**.",
                    _logger);
            }
            else
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "❌ Failed to update deduplication window.",
                    _logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster set-dedup command.");
            throw;
        }
    }
}
