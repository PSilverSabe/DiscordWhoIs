using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Extensions;
using DiscordWhoIs.Worker.Helpers;
using DiscordWhoIs.Worker.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Services;

/// <summary>
/// Service responsible for detecting AO3 links in Discord messages and posting embeds
/// with per-channel configuration support.
/// </summary>
public sealed class FanficEmbedResponderService(
    IFanficRepository fanficRepository,
    IEmbedPosterConfigurationRepository channelConfigRepository,
    IServerRepository serverRepository,
    IMemoryCache cache,
    ILogger<FanficEmbedResponderService> logger)
{
    private readonly IFanficRepository _fanficRepository = fanficRepository;
    private readonly IEmbedPosterConfigurationRepository _channelConfigRepository = channelConfigRepository;
    private readonly IServerRepository _serverRepository = serverRepository;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<FanficEmbedResponderService> _logger = logger;
    private static readonly TimeSpan s_configCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_invalidConfigCacheDuration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Handles incoming messages and posts embeds for detected AO3 links.
    /// </summary>
    public async Task HandleMessageAsync(SocketMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate message context
            if (message.Author.IsBot)
            {
                _logger.LogDebug("Ignoring message {MessageId} from bot {Author}.", message.Id, message.Author.Username);
                return;
            }

            if (message.Channel is not ITextChannel channel)
            {
                _logger.LogDebug("Ignoring message {MessageId} in non-text channel {Channel}.", message.Id, message.Channel.Name);
                return;
            }

            if (channel.Guild is not SocketGuild guild)
            {
                _logger.LogDebug("Ignoring message {MessageId} — guild context unavailable.", message.Id);
                return;
            }

            // Extract AO3 links from message
            IReadOnlyList<string> links = Ao3LinkDetectorHelper.ExtractLinks(message.Content);
            if (links.Count == 0)
            {
                return;
            }

            // Get configuration for this channel
            EmbedPosterConfiguration? config = await GetConfigForChannelAsync(
                guild.Id, channel.Id, cancellationToken);

            if (config is null || !config.Enabled)
            {
                _logger.LogDebug(
                    "EmbedPoster is disabled for server {DiscordServerId} in #{Channel} — ignoring AO3 links in message {MessageId}.",
                    guild.Id, channel.Name, message.Id);
                return;
            }

            _logger.LogInformation(
                "Detected {Count} AO3 link(s) in message {MessageId} from {Author} in server {DiscordServerId} #{Channel}",
                links.Count, message.Id, message.Author.Username, guild.Id, channel.Name);

            TimeSpan deduplicationWindow = TimeSpan.FromMinutes(config.DeduplicationWindowMinutes);

            // Process each link
            foreach (string link in links)
            {
                await TryPostEmbedForLinkAsync(channel, link, deduplicationWindow, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while handling message {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// Gets the configuration for a specific server and channel.
    /// </summary>
    private async Task<EmbedPosterConfiguration?> GetConfigForChannelAsync(
        ulong serverId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"embedposter:config:{serverId}:{channelId}";

        if (_cache.TryGetValue(cacheKey, out EmbedPosterConfiguration? cached))
        {
            return cached;
        }

        try
        {
            // Get server by Discord ID
            Server? server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
            if (server is null)
            {
                _logger.LogDebug("Server {DiscordServerId} not found in database", serverId);
                _cache.Set<EmbedPosterConfiguration?>(cacheKey, null, s_invalidConfigCacheDuration);
                return null;
            }

            // Fetch channel configuration
            EmbedPosterConfiguration? config = await _channelConfigRepository.GetByServerAndChannelAsync(
                server.Id, channelId);

            if (config is not null)
            {
                _cache.Set(cacheKey, config, s_configCacheDuration);
            }
            else
            {
                // Cache null result to avoid repeated database queries
                _cache.Set<EmbedPosterConfiguration?>(cacheKey, null, s_invalidConfigCacheDuration);
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving embed poster configuration for server {DiscordServerId} channel {ChannelId}",
                serverId, channelId);
            return null;
        }
    }

    /// <summary>
    /// Invalidates the cache for a specific server's configuration.
    /// </summary>
    public void InvalidateServerConfigCache(ulong serverId)
    {
        try
        {
            string cacheKeyPattern = $"embedposter:config:{serverId}:";

            var keysToRemove = _cache.GetCacheKeys()
                .Where(k => k.StartsWith(cacheKeyPattern))
                .ToList();

            foreach (string? key in keysToRemove)
            {
                _cache.Remove(key);
                _logger.LogDebug("Invalidated cache for key {CacheKey}", key);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogInformation(
                    "Invalidated {Count} cache entries for server {DiscordServerId}",
                    keysToRemove.Count, serverId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating server config cache for server {DiscordServerId}", serverId);
        }
    }

    /// <summary>
    /// Attempts to post an embed for the given AO3 link.
    /// </summary>
    private async Task TryPostEmbedForLinkAsync(
        ITextChannel channel,
        string link,
        TimeSpan deduplicationWindow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string normalisedLink = NormaliseAo3Link(link);
            string cacheKey = $"embed:{channel.GuildId}:{channel.Id}:{normalisedLink}";

            // Check if this link was already posted in this channel recently
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogDebug(
                    "Skipping duplicate embed for {Link} in server {DiscordServerId} #{Channel} — within deduplication window.",
                    normalisedLink, channel.GuildId, channel.Name);
                return;
            }

            // Mark link as posted before fetching (to avoid race conditions)
            _cache.Set(cacheKey, true, deduplicationWindow);

            // Fetch fanfic from database
            Fanfic? fic = await _fanficRepository.GetByLinkAsync(normalisedLink);

            if (fic is null)
            {
                _logger.LogDebug("AO3 link {Link} not found in database — skipping embed.", normalisedLink);
                return;
            }

            _logger.LogInformation(
                "Posting embed for fic '{Title}' ({Link}) in server {DiscordServerId} #{Channel}",
                fic.Title, normalisedLink, channel.GuildId, channel.Name);

            // Post the embed
            Embed embed = FanficEmbedBuilderHelper.Build(fic);
            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post embed for link {Link} in channel {ChannelId}", link, channel.Id);
        }
    }

    /// <summary>
    /// Normalizes an AO3 link to a consistent format for comparison.
    /// </summary>
    private static string NormaliseAo3Link(string link)
    {
        int worksIndex = link.IndexOf("/works/", StringComparison.OrdinalIgnoreCase);
        if (worksIndex == -1)
        {
            return link;
        }

        int idStart = worksIndex + "/works/".Length;
        int idEnd = link.IndexOf('/', idStart);

        return idEnd == -1 ? link : link[..idEnd];
    }
}
