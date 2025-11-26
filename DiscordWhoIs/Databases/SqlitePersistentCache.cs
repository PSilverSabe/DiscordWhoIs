using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Databases.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DiscordWhoIs.Databases
{
    public sealed class SqlitePersistentCache : IPersistentCache, IHostedService, IDisposable
    {
        private readonly IDbContextFactory<CacheDbContext> _dbContextFactory;
        private readonly ILogger<SqlitePersistentCache> _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<(string key, CacheEntry? entry, bool isDelete)> _ops = new();
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _cleanupInterval;
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;

        public SqlitePersistentCache(
            IDbContextFactory<CacheDbContext> dbContextFactory,
            ILogger<SqlitePersistentCache> logger,
            IConfiguration config)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            _flushInterval = int.TryParse(config?["Cache:FlushIntervalSeconds"], out var fi) && fi > 0
                ? TimeSpan.FromSeconds(fi) : TimeSpan.FromSeconds(5);

            _cleanupInterval = int.TryParse(config?["Cache:CleanupIntervalSeconds"], out var ci) && ci > 0
                ? TimeSpan.FromSeconds(ci) : TimeSpan.FromMinutes(1);

            // Load entries from DB
            using var context = _dbContextFactory.CreateDbContext();
            context.Database.EnsureCreated();
            var now = DateTime.UtcNow;

            foreach (var entry in context.CacheEntries
                         .AsNoTracking()
                         .Where(e => !e.ExpiresAt.HasValue || e.ExpiresAt > now))
            {
                _store[entry.Key] = entry;
            }
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;

            if (!_store.TryGetValue(key, out var entry)) return false;

            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                _store.TryRemove(key, out _);
                _ops.Enqueue((key, null, true));
                return false;
            }

            try
            {
                value = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(entry.Json);
                return value != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cache entry {Key}; removing it.", key);
                _store.TryRemove(key, out _);
                _ops.Enqueue((key, null, true));
                return false;
            }
        }

        public void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null)
        {
            if (string.IsNullOrEmpty(key)) return;

            var entry = new CacheEntry
            {
                Key = key,
                TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
                Json = Newtonsoft.Json.JsonConvert.SerializeObject(value),
                ExpiresAt = absoluteExpirationRelativeToNow.HasValue
                    ? DateTime.UtcNow + absoluteExpirationRelativeToNow.Value
                    : null
            };

            _store[key] = entry;
            _ops.Enqueue((key, entry, false));
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _store.TryRemove(key, out _);
            _ops.Enqueue((key, null, true));
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            var pending = new Dictionary<string, (CacheEntry? entry, bool isDelete)>(StringComparer.Ordinal);
            while (_ops.TryDequeue(out var op))
                pending[op.key] = (op.entry, op.isDelete);

            if (pending.Count == 0) return;

            await _dbLock.WaitAsync(cancellationToken);
            try
            {
                await using var context = _dbContextFactory.CreateDbContext();

                foreach (var kv in pending)
                {
                    if (kv.Value.isDelete)
                    {
                        var entity = await context.CacheEntries.FindAsync(new object[] { kv.Key }, cancellationToken);
                        if (entity != null)
                            context.CacheEntries.Remove(entity);
                    }
                    else if (kv.Value.entry != null)
                    {
                        var existing = await context.CacheEntries.FindAsync(new object[] { kv.Key }, cancellationToken);
                        if (existing != null)
                        {
                            // Update properties
                            existing.TypeName = kv.Value.entry.TypeName;
                            existing.Json = kv.Value.entry.Json;
                            existing.ExpiresAt = kv.Value.entry.ExpiresAt;
                        }
                        else
                        {
                            await context.CacheEntries.AddAsync(kv.Value.entry, cancellationToken);
                        }
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task RemoveExpiredAsync(CancellationToken cancellationToken = default)
        {
            await _dbLock.WaitAsync(cancellationToken);
            try
            {
                await using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;

                var expired = await context.CacheEntries
                    .Where(e => e.ExpiresAt.HasValue && e.ExpiresAt <= now)
                    .ToListAsync(cancellationToken);

                if (expired.Count > 0)
                {
                    context.CacheEntries.RemoveRange(expired);
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Removed {Count} expired cache entries from DB", expired.Count);
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => BackgroundLoopAsync(_cts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task BackgroundLoopAsync(CancellationToken cancellationToken)
        {
            var cleanupCounter = TimeSpan.Zero;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_flushInterval, cancellationToken);

                    try
                    {
                        await FlushAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing cache");
                    }

                    cleanupCounter += _flushInterval;
                    if (cleanupCounter >= _cleanupInterval)
                    {
                        cleanupCounter = TimeSpan.Zero;
                        try
                        {
                            await RemoveExpiredAsync(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error cleaning expired entries");
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await FlushAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error flushing cache during shutdown");
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();
            if (_backgroundTask != null) await _backgroundTask;
            await FlushAsync(cancellationToken);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}