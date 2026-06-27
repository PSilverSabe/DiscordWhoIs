using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Services;

// Service to maintain active user cache
public class ActiveUsersCacheService : IDisposable
{
    private readonly ConcurrentDictionary<ulong, List<(ulong UserId, DateTimeOffset Timestamp)>> _messageCache
        = new();

    private readonly Timer _pruneTimer;
    private readonly ILogger<ActiveUsersCacheService>? _logger;

    private readonly TimeSpan _maxCacheDuration = TimeSpan.FromHours(12);
    private readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(5);

    public ActiveUsersCacheService(ILogger<ActiveUsersCacheService>? logger = null)
    {
        _logger = logger;
        _logger?.LogDebug("ActiveUsersCacheService initializing. Prune interval: {Interval}", _pruneInterval);
        // Set up a timer to prune old messages periodically
        _pruneTimer = new Timer(PruneOldMessages, null, _pruneInterval, _pruneInterval);
        _logger?.LogInformation("ActiveUsersCacheService started.");
    }

    // Add a new message to the cache
    public void AddMessage(SocketMessage message)
    {
        if (!(message.Channel is ITextChannel))
        {
            return;
        }
        List<(ulong UserId, DateTimeOffset Timestamp)> list = _messageCache.GetOrAdd(message.Channel.Id, _ => new List<(ulong, DateTimeOffset)>());
        lock (list)
        {
            list.Add((message.Author.Id, message.Timestamp));
            _logger?.LogDebug("Added message from user {UserId} in channel {ChannelId} at {Timestamp}", message.Author.Id, message.Channel.Id, message.Timestamp);
        }
    }

    // Get active users in a specific channel within the last X hours
    public IEnumerable<ulong> GetActiveUsers(ulong channelId, int hours)
    {
        if (!_messageCache.TryGetValue(channelId, out List<(ulong UserId, DateTimeOffset Timestamp)>? list))
        {
            return Enumerable.Empty<ulong>();
        }

        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-hours);
        lock (list)
        {
            List<ulong> users = list.Where(x => x.Timestamp >= cutoff).Select(x => x.UserId).Distinct().ToList();
            _logger?.LogDebug("Retrieved {Count} active users for channel {ChannelId} in last {Hours} hours", users.Count, channelId, hours);
            return users;
        }
    }

    // Prune messages older than 12 hours
    private void PruneOldMessages(object? state)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - _maxCacheDuration;

        foreach (KeyValuePair<ulong, List<(ulong UserId, DateTimeOffset Timestamp)>> kvp in _messageCache)
        {
            List<(ulong UserId, DateTimeOffset Timestamp)> list = kvp.Value;
            lock (list)
            {
                int removed = list.RemoveAll(x => x.Timestamp < cutoff);
                if (removed > 0)
                {
                    _logger?.LogDebug("Pruned {Removed} messages from channel {ChannelId}", removed, kvp.Key);
                }
            }
        }
    }

    public void Dispose() => _pruneTimer.Dispose();
}
