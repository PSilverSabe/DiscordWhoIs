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
    [SlashCommand("remove", "Remove embed poster configuration for a channel")]
    public async Task RemoveAsync(
        [Summary("Channel", "The channel to remove configuration for. If not specified, uses current channel.")]
        ITextChannel? channel = null)
    {
        try
        {
            var statusLines = new List<string>();
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();
            ulong channelId = channel?.Id ?? GetChannelId();

            // Get or create server
            Server? server = await _serverRepository.GetOrCreateServerAsync(serverId);
            if (server?.Id <= 0 || server is null)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "❌ Failed to retrieve server configuration.",
                    _logger);
                return;
            }

            // Delete channel configuration
            bool success = await _channelConfigRepository.DeleteChannelConfigurationAsync(server.Id, channelId);

            if (success)
            {
                _fanficEmbedResponderService.InvalidateChannelConfigCache(serverId, channelId);

                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    $"✅ Configuration removed for <#{channelId}>.",
                    _logger);
            }
            else
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    $"⚠️ No configuration found for <#{channelId}>.",
                    _logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster remove command.");
            throw;
        }
    }
}
