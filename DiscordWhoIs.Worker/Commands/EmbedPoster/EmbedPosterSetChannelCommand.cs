using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("set-channel", "Set the channel the embed poster will respond in for this server")]
    public async Task SetChannelAsync(
        [Summary("Channel", "The text channel to post embeds in. Leave empty to respond in any channel.")]
        ITextChannel? channel = null)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();

            // Ensure server configuration exists
            await _configRepository.GetOrCreateServerConfigAsync(serverId);

            // Update channel
            await _configRepository.UpdateServerChannelAsync(serverId, channel?.Id);
            _fanficEmbedResponderService.InvalidateServerConfigCache(serverId);

            string message = channel is null
                ? "✅ Embed poster will now respond in **any channel** on this server."
                : $"✅ Embed poster channel set to {channel.Mention} for this server.";

            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines, message, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster set-channel command.");
            throw;
        }
    }
}
