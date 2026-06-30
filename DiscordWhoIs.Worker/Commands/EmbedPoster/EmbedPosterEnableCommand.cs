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
    [SlashCommand("enable", "Enable embed poster for a channel")]
    public async Task EnableAsync(
        [Summary("Channel", "The channel to enable embed poster for. If not specified, uses current channel.")]
        ITextChannel? channel = null)
        => await SetChannelEnabledStateAsync(channel, enabled: true);

    [SlashCommand("disable", "Disable embed poster for a channel")]
    public async Task DisableAsync(
        [Summary("Channel", "The channel to disable embed poster for. If not specified, uses current channel.")]
        ITextChannel? channel = null)
        => await SetChannelEnabledStateAsync(channel, enabled: false);

    /// <summary>
    /// Sets the enabled/disabled state for the embed poster in the specified channel.
    /// </summary>
    private async Task SetChannelEnabledStateAsync(ITextChannel? channel, bool enabled)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();
            ulong channelId = channel?.Id ?? GetChannelId();

            // Get or create server
            Server server = await _serverRepository.GetOrCreateServerAsync(serverId);

            // Update channel configuration
            bool success = await _channelConfigRepository.UpsertChannelConfigurationAsync(
                server.Id, channelId, enabled: enabled);

            if (success)
            {
                _fanficEmbedResponderService.InvalidateServerConfigCache(serverId);

                string status = enabled ? "✅ **enabled**" : "⛔ **disabled**";
                string channelMention = $"<#{channelId}>";
                string message = $"{status} for {channelMention}.";

                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    $"Embed poster {message}",
                    _logger);
            }
            else
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "❌ Failed to update embed poster configuration.",
                    _logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing embed poster enable/disable command.");
            throw;
        }
    }
}
