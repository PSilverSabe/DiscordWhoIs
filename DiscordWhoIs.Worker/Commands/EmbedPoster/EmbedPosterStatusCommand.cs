using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.DbModels;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public partial class EmbedPosterCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("status", "Show the embed poster configuration for this server and channels")]
    public async Task StatusAsync()
    {
        try
        {
            await DeferAsync(ephemeral: true);

            ulong serverId = GetServerId();

            // Get server by Discord ID
            Server server = await _serverRepository.GetOrCreateServerAsync(serverId);

            if (server is null)
            {
                await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "No embed poster configuration found for this server. Use `/embed-poster enable` in a channel to create one.";
                });
                return;
            }

            // Get all channel configurations for this server
            var channelConfigs = (await _channelConfigRepository.GetByServerIdAsync(server.Id)).ToList();

            if (!channelConfigs.Any())
            {
                await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "No embed poster configuration found for this server. Use `/embed-poster enable` in a channel to create one.";
                });
                return;
            }

            // Build embeds for channel configurations
            var embeds = new List<Embed>();
            embeds.AddRange(BuildChannelConfigEmbeds(channelConfigs));

            // Send the embeds (Discord limits to 10 embeds per message)
            await SendEmbeds(embeds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing /embed-poster status command.");
            throw;
        }
    }

    /// <summary>
    /// Builds embeds for each channel-specific configuration.
    /// </summary>
    private IEnumerable<Embed> BuildChannelConfigEmbeds(IEnumerable<EmbedPosterConfiguration> channelConfigs)
    {
        var embeds = new List<Embed>();
        var configs = channelConfigs.ToList();

        if (configs.Count <= 5)
        {
            // Show each channel in its own embed
            foreach (EmbedPosterConfiguration config in configs)
            {
                embeds.Add(BuildChannelConfigEmbed(config));
            }
        }
        else
        {
            // Combine multiple channels in one embed for readability
            embeds.Add(BuildCombinedChannelConfigEmbed(configs));
        }

        return embeds;
    }

    /// <summary>
    /// Builds an embed for a single channel configuration.
    /// </summary>
    private Embed BuildChannelConfigEmbed(EmbedPosterConfiguration config)
    {
        string channelMention = $"<#{config.ChannelId}>";

        return new EmbedBuilder()
            .WithTitle($"#️⃣ Channel: {channelMention}")
            .WithColor(config.Enabled ? Color.Green : Color.Red)
            .AddField("Status", config.Enabled ? "✅ Enabled" : "⛔ Disabled", inline: true)
            .AddField("Deduplication Window", $"{config.DeduplicationWindowMinutes} minute(s)", inline: true)
            .Build();
    }

    /// <summary>
    /// Builds a combined embed showing multiple channel configurations.
    /// </summary>
    private Embed BuildCombinedChannelConfigEmbed(IEnumerable<EmbedPosterConfiguration> channelConfigs)
    {
        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle("#️⃣ Channel Configurations")
            .WithColor(Color.Blue);

        var enabledChannels = new StringBuilder();
        var disabledChannels = new StringBuilder();

        foreach (EmbedPosterConfiguration config in channelConfigs.OrderBy(c => c.ChannelId))
        {
            string channelMention = $"<#{config.ChannelId}>";
            string dedup = $" (Dedup: {config.DeduplicationWindowMinutes}m)";

            if (config.Enabled)
            {
                enabledChannels.AppendLine($"✅ {channelMention}{dedup}");
            }
            else
            {
                disabledChannels.AppendLine($"⛔ {channelMention}{dedup}");
            }
        }

        if (enabledChannels.Length > 0)
        {
            embedBuilder.AddField("Enabled Channels", enabledChannels.ToString().Trim(), inline: false);
        }

        if (disabledChannels.Length > 0)
        {
            embedBuilder.AddField("Disabled Channels", disabledChannels.ToString().Trim(), inline: false);
        }

        return embedBuilder.Build();
    }

    /// <summary>
    /// Sends multiple embeds to the user, respecting Discord's embed limit.
    /// </summary>
    private async Task SendEmbeds(IList<Embed> embeds)
    {
        const int maxEmbedsPerMessage = 10;

        // Send first batch of embeds
        var firstBatch = embeds.Take(maxEmbedsPerMessage).ToList();
        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = embeds.Count > maxEmbedsPerMessage
                ? $"Showing {firstBatch.Count} of {embeds.Count} configurations (limited by Discord)"
                : string.Empty;
            msg.Embeds = firstBatch.ToArray();
        });

        // If there are more embeds than Discord allows, send them as follow-up messages
        for (int i = maxEmbedsPerMessage; i < embeds.Count; i += maxEmbedsPerMessage)
        {
            var batch = embeds.Skip(i).Take(maxEmbedsPerMessage).ToList();
            await Context.Interaction.FollowupAsync(embeds: batch.ToArray(), ephemeral: true);
        }
    }
}
