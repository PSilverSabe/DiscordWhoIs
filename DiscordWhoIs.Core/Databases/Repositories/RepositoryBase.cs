using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public abstract class RepositoryBase<TContext, TLogger>
        where TContext : DbContext
    {
        protected readonly IDbContextFactory<TContext> _dbContextFactory;
        protected readonly ILogger<TLogger> _logger;

        protected RepositoryBase(IDbContextFactory<TContext> dbContextFactory, ILogger<TLogger> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            // Initialize DB and run migrations
            using var context = _dbContextFactory.CreateDbContext();
            try
            {
                context.Database.Migrate(); // Ensures DB + tables exist
                _logger.LogInformation("Database migration complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Save changes and checkpoint WAL (sync)
        /// </summary>
        protected int SaveChanges(TContext context)
        {
            int result = context.SaveChanges();
            _logger.LogDebug("Database changes saved, checkpointing WAL...");
            CheckpointWal(context);
            return result;
        }

        /// <summary>
        /// Save changes and checkpoint WAL (async)
        /// </summary>
        protected async Task<int> SaveChangesAsync(TContext context)
        {
            int result = await context.SaveChangesAsync();
            _logger.LogDebug("Database changes saved, checkpointing WAL...");
            await CheckpointWalAsync(context);
            return result;
        }

        private void CheckpointWal(TContext context)
        {
            var connection = context.Database.GetDbConnection();
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                cmd.ExecuteNonQuery();
                _logger.LogDebug("WAL checkpoint completed (sync).");
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open)
                    connection.Close();
            }
        }

        private async Task CheckpointWalAsync(TContext context)
        {
            var connection = context.Database.GetDbConnection();
            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                await cmd.ExecuteNonQueryAsync();
                _logger.LogDebug("WAL checkpoint completed (async).");
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open)
                    connection.Close();
            }
        }
    }

}
