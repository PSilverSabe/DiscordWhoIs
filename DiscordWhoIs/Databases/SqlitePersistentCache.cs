using DiscordWhoIs.Databases.DataModels;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Databases.DbModels;
using DiscordWhoIs.Databases.Serializers;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
using DiscordWhoIs.Configuration.Models;

namespace DiscordWhoIs.Databases
{
    public class SqlitePersistentCache : IPersistentCache, IHostedService, IDisposable
    {
        private readonly IDbContextFactory<CacheDbContext> _dbContextFactory;
        private readonly ILogger<SqlitePersistentCache> _logger;
        private readonly CacheConfiguration _cacheConfig;

        private readonly ConcurrentDictionary<string, CacheEntry> _store;
        private readonly ConcurrentQueue<(string key, CacheEntry? entry, bool isDelete)> _ops = new();
        private readonly SemaphoreSlim _dbLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;

        public SqlitePersistentCache(
            IDbContextFactory<CacheDbContext> dbContextFactory,
            ILogger<SqlitePersistentCache> logger,
            CacheConfiguration config)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _cacheConfig = config;
            _store = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);

            LoadInitialCacheAsync().GetAwaiter().GetResult();
        }

        public void LogInMemoryCacheState()
        {
            if (_store.IsEmpty)
            {
                _logger.LogInformation("In-memory cache is empty.");
                return;
            }

            _logger.LogInformation("In-memory cache contains {Count} entries:", _store.Count);
            foreach (var kv in _store)
            {
                _logger.LogInformation("Key: '{Key}', ExpiresAt: {ExpiresAt}", kv.Key, kv.Value.ExpiresAt);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _dbLock.Dispose();
        }

        public bool TryGetValue(string key, out IEnumerable<FicInfo> value)
        {
            value = [];
            key = NormalizeKey(key);

            if (!_store.TryGetValue(key, out var entry))
            {
                _logger.LogInformation("Key '{Key}' not found in in-memory cache", key);
                return false;
            }

            if (entry.ExpiresAt <= DateTime.UtcNow)
            {
                _store.TryRemove(key, out _);
                _ops.Enqueue((key, null, true));
                _logger.LogInformation("Key '{Key}' expired and removed from in-memory cache", key);
                return false;
            }

            try
            {
                value = JsonSerializer.Deserialize(entry.Json, CacheJsonContext.Default.IEnumerableFicInfo) ?? [];
                _logger.LogInformation("Retrieved key '{Key}' from in-memory cache", key);
                return value.Any();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cache entry {Key}; removing it.", key);
                _store.TryRemove(key, out _);
                _ops.Enqueue((key, null, true));
                return false;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(() => BackgroundLoopAsync(_cts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async void SetAsync(string key, IEnumerable<FicInfo> value, TimeSpan absoluteExpirationRelativeToNow)
        {
            key = NormalizeKey(key);

            var entry = new CacheEntry
            {
                Key = key,
                Json = JsonSerializer.Serialize(value, CacheJsonContext.Default.IEnumerableFicInfo),
                ExpiresAt = DateTime.UtcNow + absoluteExpirationRelativeToNow
            };

            _store[key] = entry;
            _ops.Enqueue((key, entry, false));
            await using (var context = _dbContextFactory.CreateDbContext())
            {
                context.CacheEntries.Add(entry);
                context.SaveChanges();
            }

            _logger.LogInformation("Set key '{Key}' in cache with expiration {ExpiresAt}", key, entry.ExpiresAt);
        }

        public async Task RemoveAsync(string key)
        {
            key = NormalizeKey(key);

            if (_store.TryRemove(key, out _))
            {
                _logger.LogInformation("Removed key '{Key}' from in-memory cache", key);
            }
            else
            {
                _logger.LogInformation("Key '{Key}' not found in in-memory cache", key);
            }

            _ops.Enqueue((key, null, true));

            try
            {
                await FlushAsync();
                _logger.LogInformation("Flush completed for key '{Key}'", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush key '{Key}'", key);
                throw;
            }

            await using var context = _dbContextFactory.CreateDbContext();
            var entities = await context.CacheEntries.Where(e => e.Key == key).ToListAsync();
            if (entities.Count > 0)
            {
                context.CacheEntries.RemoveRange(entities);
                await context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} entries for key '{Key}' directly from DB", entities.Count, key);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            var opsToProcess = new List<(string key, CacheEntry? entry, bool isDelete)>();
            while (_ops.TryDequeue(out var op))
                opsToProcess.Add(op);

            if (opsToProcess.Count == 0) return;

            await _dbLock.WaitAsync(cancellationToken);
            try
            {
                await using var context = _dbContextFactory.CreateDbContext();

                foreach (var (key, entry, isDelete) in opsToProcess)
                {
                    if (isDelete)
                    {
                        var entity = await context.CacheEntries.FindAsync([key], cancellationToken);
                        if (entity != null)
                        {
                            context.CacheEntries.Remove(entity);
                            _logger.LogInformation("Deleted key '{Key}' from persistent cache", key);
                        }
                    }
                    else if (entry != null)
                    {
                        var existing = await context.CacheEntries.FindAsync([key], cancellationToken);
                        if (existing != null)
                        {
                            existing.Json = entry.Json;
                            existing.ExpiresAt = entry.ExpiresAt;
                            _logger.LogInformation("Updated key '{Key}' in persistent cache", key);
                        }
                        else
                        {
                            await context.CacheEntries.AddAsync(entry, cancellationToken);
                            _logger.LogInformation("Added key '{Key}' to persistent cache", key);
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
                    .Where(e => e.ExpiresAt <= now)
                    .ToListAsync(cancellationToken);

                if (expired.Count > 0)
                {
                    context.CacheEntries.RemoveRange(expired);
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Removed {Count} expired cache entries from DB", expired.Count);
                }

                foreach (var entry in expired)
                {
                    _store.TryRemove(NormalizeKey(entry.Key), out _);
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task LogPersistentCacheStateAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var entries = await context.CacheEntries
                .AsNoTracking()
                .OrderBy(e => e.Key)
                .ToListAsync();

            if (entries.Count == 0)
            {
                _logger.LogInformation("Persistent cache (DB) is empty.");
                return;
            }

            _logger.LogInformation("Persistent cache (DB) contains {Count} entries:", entries.Count);
            foreach (var entry in entries)
            {
                _logger.LogInformation("Key: '{Key}', ExpiresAt: {ExpiresAt}", entry.Key, entry.ExpiresAt);
            }
        }

        public async Task LogFullCacheStateAsync()
        {
            LogInMemoryCacheState();
            await LogPersistentCacheStateAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException) { }
            }
        }

        private async Task BackgroundLoopAsync(CancellationToken cancellationToken)
        {
            var cleanupCounter = TimeSpan.Zero;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_cacheConfig.FlushIntervalSeconds, cancellationToken);

                    // Flush and log
                    try
                    {
                        await FlushAsync(cancellationToken);
                        _logger.LogInformation("Cache flushed successfully.");
                        await LogFullCacheStateAsync();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing cache");
                    }

                    cleanupCounter += _cacheConfig.FlushIntervalSeconds;
                    if (cleanupCounter >= _cacheConfig.CleanupIntervalSeconds)
                    {
                        cleanupCounter = TimeSpan.Zero;
                        try
                        {
                            await RemoveExpiredAsync(cancellationToken);
                            _logger.LogInformation("Expired cache entries removed.");
                            await LogFullCacheStateAsync();
                        }
                        catch (OperationCanceledException) { break; }
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
                    _logger.LogInformation("Final flush completed during shutdown.");
                    await LogFullCacheStateAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error flushing cache during shutdown");
                }
            }
        }

        private async Task LoadInitialCacheAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;
            var entries = await context.CacheEntries
                .AsNoTracking()
                .Where(e => e.ExpiresAt > now)
                .ToListAsync();

            foreach (var entry in entries)
            {
                _store[NormalizeKey(entry.Key)] = new CacheEntry
                {
                    Key = entry.Key,
                    Json = entry.Json,
                    ExpiresAt = entry.ExpiresAt
                };
                _logger.LogInformation("Loaded key '{Key}' from DB with expiration {ExpiresAt}", entry.Key, entry.ExpiresAt);
            }
        }

        private static string NormalizeKey(string key)
        {
            return key == null ? throw new ArgumentNullException(nameof(key)) : $"{key.Trim()}";
        }
    }
}