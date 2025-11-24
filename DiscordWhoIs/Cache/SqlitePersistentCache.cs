namespace DiscordWhoIs.Cache
{
    using DiscordWhoIs.Interfaces;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SqlitePersistentCache : IPersistentCache, IHostedService, IDisposable
    {
        private sealed class CacheEntry
        {
            public string TypeName { get; set; } = string.Empty;
            public string Json { get; set; } = string.Empty;
            public DateTimeOffset? ExpiresAt { get; set; }
        }

        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ILogger<SqlitePersistentCache> _logger;
        //private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
        private readonly ConcurrentDictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<(string key, CacheEntry? entry, bool isDelete)> _ops = new();
        private readonly TimeSpan _flushInterval;
        private readonly TimeSpan _cleanupInterval;
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;
        private readonly object _dbLock = new();

        public SqlitePersistentCache(IConfiguration config, ILogger<SqlitePersistentCache> logger)
        {
            _logger = logger;

            var configured = config?["Cache:Path"]?.Trim();
            _dbPath = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "persistent_cache.sqlite")
                : Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);

            _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";

            if (int.TryParse(config?["Cache:FlushIntervalSeconds"], out var fi) && fi > 0) _flushInterval = TimeSpan.FromSeconds(fi);
            else _flushInterval = TimeSpan.FromSeconds(5);

            if (int.TryParse(config?["Cache:CleanupIntervalSeconds"], out var ci) && ci > 0) _cleanupInterval = TimeSpan.FromSeconds(ci);
            else _cleanupInterval = TimeSpan.FromMinutes(1);

            try
            {
                EnsureDatabase();
                LoadFromDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize persistent cache; starting empty.");
            }
        }

        private void EnsureDatabase()
        {
            lock (_dbLock)
            {
                var dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
CREATE TABLE IF NOT EXISTS cache_entries (
  key TEXT PRIMARY KEY,
  typeName TEXT NOT NULL,
  value TEXT NOT NULL,
  expiresAt INTEGER NULL
);
";
                cmd.ExecuteNonQuery();
            }
        }

        private void LoadFromDatabase()
        {
            lock (_dbLock)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, typeName, value, expiresAt FROM cache_entries WHERE expiresAt IS NULL OR expiresAt > strftime('%s','now');";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    var typeName = reader.GetString(1);
                    var json = reader.GetString(2);
                    DateTimeOffset? expiresAt = null;
                    if (!reader.IsDBNull(3))
                    {
                        var seconds = reader.GetInt64(3);
                        expiresAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    }
                    _store[key] = new CacheEntry { TypeName = typeName, Json = json, ExpiresAt = expiresAt };
                }
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
                value = JsonConvert.DeserializeObject<T>(entry.Json);
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
                TypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
                Json = JsonConvert.SerializeObject(value),
                ExpiresAt = absoluteExpirationRelativeToNow.HasValue ? DateTimeOffset.UtcNow + absoluteExpirationRelativeToNow.Value : null
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
            {
                pending[op.key] = (op.entry, op.isDelete);
            }

            if (pending.Count == 0) return;

            await Task.Run(() =>
            {
                lock (_dbLock)
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    using var tx = conn.BeginTransaction();
                    using var cmd = conn.CreateCommand();
                    foreach (var kv in pending)
                    {
                        if (kv.Value.isDelete)
                        {
                            cmd.CommandText = "DELETE FROM cache_entries WHERE key = @key";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@key", kv.Key);
                            cmd.ExecuteNonQuery();
                        }
                        else if (kv.Value.entry != null)
                        {
                            cmd.CommandText = "INSERT OR REPLACE INTO cache_entries (key, typeName, value, expiresAt) VALUES (@key, @typeName, @value, @expiresAt)";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@key", kv.Key);
                            cmd.Parameters.AddWithValue("@typeName", kv.Value.entry.TypeName);
                            cmd.Parameters.AddWithValue("@value", kv.Value.entry.Json);
                            if (kv.Value.entry.ExpiresAt.HasValue)
                                cmd.Parameters.AddWithValue("@expiresAt", kv.Value.entry.ExpiresAt.Value.ToUnixTimeSeconds());
                            else
                                cmd.Parameters.AddWithValue("@expiresAt", DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public Task RemoveExpiredAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                lock (_dbLock)
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM cache_entries WHERE expiresAt IS NOT NULL AND expiresAt <= strftime('%s','now');";
                    var deleted = cmd.ExecuteNonQuery();
                    if (deleted > 0) _logger.LogInformation("Removed {Count} expired cache entries from DB", deleted);

                    var now = DateTimeOffset.UtcNow;
                    foreach (var kv in _store.ToArray())
                    {
                        if (kv.Value.ExpiresAt.HasValue && kv.Value.ExpiresAt.Value <= now)
                        {
                            _store.TryRemove(kv.Key, out _);
                        }
                    }
                }
            }, cancellationToken);
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
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing cache to SQLite");
                    }

                    cleanupCounter += _flushInterval;
                    if (cleanupCounter >= _cleanupInterval)
                    {
                        cleanupCounter = TimeSpan.Zero;
                        try
                        {
                            await RemoveExpiredAsync(cancellationToken);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error cleaning expired cache entries");
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
            try
            {
                _cts?.Cancel();
                if (_backgroundTask != null) await _backgroundTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping cache background task");
            }

            await FlushAsync(cancellationToken);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
