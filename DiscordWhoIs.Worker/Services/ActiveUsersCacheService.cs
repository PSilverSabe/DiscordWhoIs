using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DiscordWhoIs.Worker.Services
{
    // Service to maintain active user cache
    public class ActiveUsersCacheService : IDisposable
    {
        private readonly ConcurrentDictionary<ulong, List<(ulong UserId, DateTimeOffset Timestamp)>> _messageCache
            = new();

        private readonly Timer _pruneTimer;

        private readonly TimeSpan _maxCacheDuration = TimeSpan.FromHours(12);
        private readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(5);

        public ActiveUsersCacheService()
        {
            // Set up a timer to prune old messages periodically
            _pruneTimer = new Timer(PruneOldMessages, null, _pruneInterval, _pruneInterval);
        }

        // Add a new message to the cache
        public void AddMessage(SocketMessage message)
        {
            if (!(message.Channel is ITextChannel)) return;

            var list = _messageCache.GetOrAdd(message.Channel.Id, _ => new List<(ulong, DateTimeOffset)>());
            lock (list)
            {
                list.Add((message.Author.Id, message.Timestamp));
            }
        }

        // Get active users in a specific channel within the last X hours
        public IEnumerable<ulong> GetActiveUsers(ulong channelId, int hours)
        {
            if (!_messageCache.TryGetValue(channelId, out var list))
                return Enumerable.Empty<ulong>();

            var cutoff = DateTimeOffset.UtcNow.AddHours(-hours);
            lock (list)
            {
                return list.Where(x => x.Timestamp >= cutoff).Select(x => x.UserId).Distinct();
            }
        }

        // Prune messages older than 12 hours
        private void PruneOldMessages(object? state)
        {
            var cutoff = DateTimeOffset.UtcNow - _maxCacheDuration;

            foreach (var kvp in _messageCache)
            {
                var list = kvp.Value;
                lock (list)
                {
                    list.RemoveAll(x => x.Timestamp < cutoff);
                }
            }
        }

        public void Dispose()
        {
            _pruneTimer.Dispose();
        }
    }
}
