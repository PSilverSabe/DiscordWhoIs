using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Worker.Commands.Helpers;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand
{
    [SlashCommand("set-channel", "Set the channel the embed poster will respond in")]
    public async Task SetChannelAsync(
        [Summary("Channel", "The text channel to post embeds in. Leave empty to respond in any channel.")]
        ITextChannel? channel = null)
    {
        var statusLines = new List<string>();
        await DeferAsync(ephemeral: true);

        await _configRepository.SetChannelAsync(channel?.Id);

        string message = channel is null
            ? "✅ Embed poster will now respond in **any channel**."
            : $"✅ Embed poster channel set to {channel.Mention}.";

        await InteractionResponseHelper.UpdateOriginalResponseAsync(
            Context.Interaction, statusLines, message, _logger);
    }
}
