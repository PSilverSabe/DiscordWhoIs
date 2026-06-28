using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using DiscordWhoIs.Worker.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Services;

public sealed class FanficEmbedResponderService(
    IFanficRepository fanficRepository,
    IEmbedPosterConfigurationRepository configRepository,
    IMemoryCache cache,
    ILogger<FanficEmbedResponderService> logger)
{
    private readonly IFanficRepository _fanficRepository = fanficRepository;
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<FanficEmbedResponderService> _logger = logger;
    private static readonly TimeSpan s_configCacheDuration = TimeSpan.FromMinutes(1);
    private const string ConfigCacheKey = "embedposter:config";

    private async Task<EmbedPosterConfiguration> GetConfigAsync()
    {
        if (_cache.TryGetValue(ConfigCacheKey, out EmbedPosterConfiguration? cached) && cached is not null)
        {
            return cached;
        }

        EmbedPosterConfiguration config = await _configRepository.GetAsync();
        _cache.Set(ConfigCacheKey, config, s_configCacheDuration);
        return config;
    }

    public async Task HandleMessageAsync(SocketMessage message)
    {
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

        IReadOnlyList<string> links = Ao3LinkDetector.ExtractLinks(message.Content);
        if (links.Count == 0)
        {
            return;
        }

        EmbedPosterConfiguration config = await GetConfigAsync();

        if (!config.Enabled)
        {
            _logger.LogDebug("EmbedPoster is disabled — ignoring AO3 links in message {MessageId}.", message.Id);
            return;
        }

        // If a specific channel is configured, only respond there
        if (config.ChannelId.HasValue && channel.Id != config.ChannelId.Value)
        {
            _logger.LogDebug(
                "Message {MessageId} is in #{Channel} which is not the configured embed channel — ignoring.",
                message.Id, channel.Name);
            return;
        }

        _logger.LogInformation(
            "Detected {Count} AO3 link(s) in message {MessageId} from {Author} in #{Channel}",
            links.Count, message.Id, message.Author.Username, channel.Name);

        TimeSpan deduplicationWindow = TimeSpan.FromMinutes(config.DeduplicationWindowMinutes);

        foreach (string link in links)
        {
            await TryPostEmbedForLinkAsync(channel, link, deduplicationWindow);
        }
    }

    private async Task TryPostEmbedForLinkAsync(ITextChannel channel, string link, TimeSpan deduplicationWindow)
    {
        _logger.LogInformation($"Post embed for link: {link}");

        string normalisedLink = NormaliseAo3Link(link);
        string cacheKey = $"embed:{channel.Id}:{normalisedLink}";

        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogDebug(
                "Skipping duplicate embed for {Link} in #{Channel} — within deduplication window.",
                normalisedLink, channel.Name);
            return;
        }

        Fanfic? fic = await _fanficRepository.GetByLinkAsync(normalisedLink);

        if (fic is null)
        {
            _cache.Set(cacheKey, true, deduplicationWindow);
            _logger.LogDebug("AO3 link {Link} not found in database — skipping embed.", normalisedLink);
            return;
        }

        _cache.Set(cacheKey, true, deduplicationWindow);

        _logger.LogInformation("Posting embed for fic '{Title}' ({Link})", fic.Title, normalisedLink);

        try
        {
            Embed embed = FanficEmbedBuilder.Build(fic);
            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _cache.Remove(cacheKey);
            _logger.LogError(ex, "Failed to post embed for fic '{Title}'", fic.Title);
        }
    }

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
