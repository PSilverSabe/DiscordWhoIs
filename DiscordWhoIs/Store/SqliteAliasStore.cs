namespace DiscordWhoIs.Store
{
    using DiscordWhoIs.Interfaces;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class SqliteAliasStore : IAliasStore, IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ILogger<SqliteAliasStore> _logger;
        private readonly ConcurrentDictionary<string, AliasEntry> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _dbLock = new();

        public SqliteAliasStore(IConfiguration configuration, ILogger<SqliteAliasStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var configured = configuration?["Aliases:Path"]?.Trim();
            _dbPath = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "aliases.sqlite")
                : Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);

            _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";

            try
            {
                EnsureDatabase();
                LoadFromDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize alias store; starting with an empty set.");
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

                // Create table (new installs). Do NOT alter existing DB schema.
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
CREATE TABLE IF NOT EXISTS aliases (
  alias TEXT PRIMARY KEY,
  real TEXT NOT NULL,
  description TEXT NULL
);
";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LoadFromDatabase()
        {
            lock (_dbLock)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Always read alias, real, description. Existing DBs without the column must be migrated externally.
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT alias, real, description FROM aliases;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var alias = reader.GetString(0);
                    var real = reader.GetString(1);
                    string? description = null;
                    if (!reader.IsDBNull(2)) description = reader.GetString(2);
                    var entry = new AliasEntry(alias, real, description);
                    _store[alias] = entry;
                }
            }
        }

        public IReadOnlyList<AliasEntry> GetAllAliases() => _store.Values.ToList();

        public bool TryResolve(string alias, out string real)
        {
            real = default!;
            if (string.IsNullOrWhiteSpace(alias)) return false;
            if (_store.TryGetValue(alias.Trim(), out var entry))
            {
                real = entry.Real;
                return true;
            }
            return false;
        }

        public bool TryGet(string alias, out AliasEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(alias)) return false;
            return _store.TryGetValue(alias.Trim(), out entry);
        }

        public void AddOrUpdate(string alias, string real, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("Alias cannot be empty.", nameof(alias));
            if (string.IsNullOrWhiteSpace(real)) throw new ArgumentException("Real username cannot be empty.", nameof(real));

            alias = alias.Trim();
            real = real.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            lock (_dbLock)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Persist alias mapping with description column (assumed present).
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO aliases (alias, real, description) VALUES (@alias, @real, @description);";
                cmd.Parameters.AddWithValue("@alias", alias);
                cmd.Parameters.AddWithValue("@real", real);
                if (description != null) cmd.Parameters.AddWithValue("@description", description);
                else cmd.Parameters.AddWithValue("@description", DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            var entry = new AliasEntry(alias, real, description);
            _store[alias] = entry;
            _logger.LogInformation("Added/updated alias {Alias} -> {Real} (desc: {Desc})", alias, real, description ?? "<none>");
        }

        public bool Remove(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias)) return false;
            alias = alias.Trim();

            lock (_dbLock)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM aliases WHERE alias = @alias;";
                cmd.Parameters.AddWithValue("@alias", alias);
                var deleted = cmd.ExecuteNonQuery();
                _store.TryRemove(alias, out _);
                if (deleted > 0)
                {
                    _logger.LogInformation("Removed alias {Alias} from DB", alias);
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            // nothing to dispose currently
        }
    }
}