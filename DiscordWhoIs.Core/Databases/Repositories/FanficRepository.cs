using CsvHelper;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public class FanficRepository : IFanficRepository
    {
        private readonly IDbContextFactory<BotDbContext> _dbContextFactory;
        private readonly ILogger<FanficRepository> _logger;
        public FanficRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<FanficRepository> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            using var context = _dbContextFactory.CreateDbContext();
            try
            {
                context.Database.EnsureCreated(); // Creates DB + Aliases table if missing
                // Load existing fanfics
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
            using var context = _dbContextFactory.CreateDbContext();
            IReadOnlyList<Fanfic> results = [.. context.Fanfics.AsNoTracking()];
            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string author)
        {
            using var context = _dbContextFactory.CreateDbContext();
            IReadOnlyList<Fanfic> results = [.. context.Fanfics.AsNoTracking()
                                                .Where(f => f.Author.ToLower() == author.ToLower() 
                                                            || f.Author.ToLower().Contains(author.ToLower()))];
            return Task.FromResult(results);
        }

        public Task<Fanfic?> GetByIdAsync(int id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return Task.FromResult(context.Fanfics.AsNoTracking().FirstOrDefault(f => f.Id == id));
        }

        public Task<Fanfic?> GetByTitleAsync(string title)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return Task.FromResult(context.Fanfics.AsNoTracking().FirstOrDefault(f => f.Title == title));
        }

        public Task<bool> ImportFromCsvAsync(string csvFileName)
        {
            var csvFileExists = File.Exists(csvFileName);
            if (!csvFileExists)
            {
                return Task.FromResult(false);
            }

            var parsedContent = new List<Fanfic>();
            using (var reader = new StreamReader(csvFileName))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
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
                var existingFanfics = context.Fanfics.AsNoTracking().ToDictionary(f => f.Link, StringComparer.OrdinalIgnoreCase);
                foreach (var fanfic in parsedContent)
                {
                    if (existingFanfics.TryGetValue(fanfic.Link, out Fanfic? existingFanfic))
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
                context.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }

            return Task.FromResult(true);
        }
    }
}
