using CsvHelper;
using CsvHelper.Configuration;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public class FanficRepository : IFanficRepository
    {
        private readonly IDbContextFactory<BotDbContext> _dbContextFactory;
        private readonly ILogger<FanficRepository> _logger;
        private readonly ConcurrentDictionary<string, Fanfic> _store = new(StringComparer.OrdinalIgnoreCase);
        public FanficRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<FanficRepository> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            using var context = _dbContextFactory.CreateDbContext();
            try
            {
                context.Database.EnsureCreated(); // Creates DB + Aliases table if missing

                // Load existing fanfics
                SetLocalStore(context);
                context.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }
        }


        public Task<IReadOnlyList<Fanfic>> GetAllAsync()
        {
            return Task.FromResult((IReadOnlyList<Fanfic>)[.. _store.Values]);
        }

        public Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string author)
        {
            IReadOnlyList<Fanfic> directSearch = [.. _store.Values.Where(f => f.Author.Equals(author, StringComparison.OrdinalIgnoreCase))];
            IReadOnlyList<Fanfic> pseudSearch = [.. _store.Values.Where(f => f.Author.Contains($"({author})", StringComparison.OrdinalIgnoreCase))];
            IReadOnlyList<Fanfic> results = [..directSearch, ..pseudSearch];

            return Task.FromResult(results);
        }

        public Task<Fanfic?> GetByIdAsync(int id)
        {
            return Task.FromResult(_store.Values.FirstOrDefault(f => f.Id == id));
        }

        public Task<Fanfic?> GetByTitleAsync(string title)
        {
            return Task.FromResult(_store.TryGetValue(title, out var fanfic) ? fanfic : null);
        }

        public Task<bool> ImportFromCsvAsync(string csvFileName)
        {
            var csvFileExists = File.Exists(csvFileName);
            if (!csvFileExists)
            {
                return Task.FromResult(false);
            }

            var parsedContent = new List<Fanfic>();
            using(var reader = new StreamReader(csvFileName))
            using(var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                parsedContent.AddRange(csv.GetRecords<Fanfic>());
            }

            if (parsedContent.Count == 0)
            {
                return Task.FromResult(false);
            }

            using var context = _dbContextFactory.CreateDbContext();

            try
            {
                var existingFanfics = context.Fanfics.AsNoTracking().ToDictionary(f => f.Title, StringComparer.OrdinalIgnoreCase);
                foreach (var fanfic in parsedContent)
                {
                    if (existingFanfics.TryGetValue(fanfic.Title, out Fanfic? existingFanfic))
                    {
                        fanfic.Id = existingFanfic.Id; // Preserve the ID for update
                        context.Entry(existingFanfic).CurrentValues.SetValues(fanfic);
                    }
                    else
                    {
                        // New entry
                        context.Fanfics.Add(fanfic);
                    }
                }

                context.SaveChanges();
                SetLocalStore(context);
                context.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }

            return Task.FromResult(true); 
        }

        private void SetLocalStore<T>(T dbContext) 
            where T : BotDbContext
        {
            // Load existing fanfics
            foreach (var entry in dbContext.Fanfics.AsNoTracking())
            {
                _store[entry.Title] = entry;
            }
        }
    }
}
