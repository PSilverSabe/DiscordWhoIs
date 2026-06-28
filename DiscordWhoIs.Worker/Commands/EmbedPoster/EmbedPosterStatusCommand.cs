using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
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
