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

            // Get all configurations for this server (server + all channels)
            IEnumerable<EmbedPosterConfiguration> configurations = await _configRepository.GetAllByServerIdAsync(serverId);
            var configList = configurations.ToList();

            if (!configList.Any())
            {
                await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "No embed poster configuration found for this server. Use `/embed-poster enable` to create one.";
                });
                return;
            }

            // Separate server and channel configurations
            EmbedPosterConfiguration? serverConfig = configList.FirstOrDefault(c => c.ChannelId == null);
            var channelConfigs = configList.Where(c => c.ChannelId.HasValue).ToList();

            // Build the main embed for server configuration
            Embed serverEmbed = BuildServerConfigEmbed(serverConfig);

            // If there are channel-specific configurations, create additional embeds
            var embeds = new List<Embed> { serverEmbed };

            if (channelConfigs.Any())
            {
                embeds.AddRange(BuildChannelConfigEmbeds(channelConfigs));
            }

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
    /// Builds an embed displaying the server-level configuration.
    /// </summary>
    private Embed BuildServerConfigEmbed(EmbedPosterConfiguration? serverConfig)
    {
        if (serverConfig is null)
        {
            return new EmbedBuilder()
                .WithTitle("🖥️ Server Configuration")
                .WithColor(Color.LightGrey)
                .WithDescription("No server-level configuration found.")
                .Build();
        }

        string channelDisplay = serverConfig.ChannelId.HasValue
            ? $"<#{serverConfig.ChannelId}>"
            : "Any channel (default)";

        return new EmbedBuilder()
            .WithTitle("🖥️ Server Configuration")
            .WithColor(serverConfig.Enabled ? Color.Green : Color.Red)
            .AddField("Status", serverConfig.Enabled ? "✅ Enabled" : "⛔ Disabled", inline: true)
            .AddField("Default Channel", channelDisplay, inline: true)
            .AddField("Deduplication Window", $"{serverConfig.DeduplicationWindowMinutes} minute(s)", inline: false)
            .Build();
    }

    /// <summary>
    /// Builds embeds for each channel-specific configuration.
    /// </summary>
    private IEnumerable<Embed> BuildChannelConfigEmbeds(IEnumerable<EmbedPosterConfiguration> channelConfigs)
    {
        var embeds = new List<Embed>();

        // Group configs if there are many channels
        var configs = channelConfigs.ToList();

        if (configs.Count <= 5)
        {
            // Show each channel in its own embed
            foreach (EmbedPosterConfiguration? config in configs)
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
        string channelMention = config.ChannelId.HasValue ? $"<#{config.ChannelId}>" : "Unknown";

        return new EmbedBuilder()
            .WithTitle($"#️⃣ Channel Override: {channelMention}")
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
            .WithTitle("#️⃣ Channel Overrides")
            .WithColor(Color.Blue);

        var enabledChannels = new StringBuilder();
        var disabledChannels = new StringBuilder();

        foreach (EmbedPosterConfiguration? config in channelConfigs.OrderBy(c => c.ChannelId))
        {
            string channelMention = config.ChannelId.HasValue ? $"<#{config.ChannelId}>" : "Unknown";
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
            embedBuilder.AddField("Enabled Overrides", enabledChannels.ToString().Trim(), inline: false);
        }

        if (disabledChannels.Length > 0)
        {
            embedBuilder.AddField("Disabled Overrides", disabledChannels.ToString().Trim(), inline: false);
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
