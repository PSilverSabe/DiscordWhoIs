using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public class EmbedPosterStatusCommand(
    IEmbedPosterConfigurationRepository configRepository,
    ILogger<EmbedPosterStatusCommand> logger)
    : EmbedPosterCommandGroup
{
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly ILogger<EmbedPosterStatusCommand> _logger = logger;

    [SlashCommand("status", "Show the current embed poster configuration")]
    public async Task StatusAsync()
    {
        await DeferAsync(ephemeral: true);

        EmbedPosterConfiguration config = await _configRepository.GetAsync();

        string channelDisplay = config.ChannelId.HasValue
            ? $"<#{config.ChannelId}>"
            : "Any channel";

        Embed embed = new EmbedBuilder()
            .WithTitle("Embed Poster Configuration")
            .WithColor(config.Enabled ? Color.Green : Color.Red)
            .AddField("Status", config.Enabled ? "✅ Enabled" : "⛔ Disabled", inline: true)
            .AddField("Channel", channelDisplay, inline: true)
            .AddField("Deduplication Window", $"{config.DeduplicationWindowMinutes} minute(s)", inline: true)
            .Build();

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = string.Empty;
            msg.Embed = embed;
        });
    }
}
