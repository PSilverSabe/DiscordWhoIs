using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public class EmbedPosterSetChannelCommand(
    IEmbedPosterConfigurationRepository configRepository,
    ILogger<EmbedPosterSetChannelCommand> logger)
    : EmbedPosterCommandGroup
{
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly ILogger<EmbedPosterSetChannelCommand> _logger = logger;

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
