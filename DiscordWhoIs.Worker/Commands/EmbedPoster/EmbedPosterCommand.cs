using System;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Services;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

[Group("embed-poster", "Configure the AO3 embed poster")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public partial class EmbedPosterCommand(
    IEmbedPosterConfigurationRepository channelConfigRepository,
    IServerRepository serverRepository,
    ILogger<EmbedPosterCommand> logger,
    FanficEmbedResponderService fanficEmbedResponderService) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEmbedPosterConfigurationRepository _channelConfigRepository = channelConfigRepository;
    private readonly IServerRepository _serverRepository = serverRepository;
    private readonly ILogger<EmbedPosterCommand> _logger = logger;
    private readonly FanficEmbedResponderService _fanficEmbedResponderService = fanficEmbedResponderService;

    /// <summary>
    /// Gets the Discord server ID from the current interaction context.
    /// </summary>
    private ulong GetServerId() => Context.Guild?.Id ?? throw new InvalidOperationException("This command must be used in a server.");

    /// <summary>
    /// Gets the Discord channel ID from the current interaction context.
    /// </summary>
    private ulong GetChannelId() => Context.Channel?.Id ?? throw new InvalidOperationException("Channel context is unavailable.");
}
