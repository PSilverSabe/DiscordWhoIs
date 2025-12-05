using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.DbModels;
using DiscordWhoIs.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DiscordWhoIs.Databases.Repositories
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
            IReadOnlyList<Fanfic> results = [.. _store.Values];
            results = [.. results.Where(f => f.Author.Equals(author, StringComparison.OrdinalIgnoreCase))];
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

            var parsedContent = File.ReadAllLines(csvFileName)
                                        .Select(static x =>
                                        {
                                            var parts = x.Split(',');
                                            return new Fanfic
                                            {
                                                Link = SafeParseCsvField(parts, 0),
                                                Title = SafeParseCsvField(parts, 1),
                                                Author = SafeParseCsvField(parts, 2),
                                                Summary = SafeParseCsvField(parts, 3),
                                                WordCount = SafeParseCsvIntField(parts, 4),
                                                HitCount = SafeParseCsvIntField(parts, 5),
                                                CommentCount = SafeParseCsvIntField(parts, 6),
                                                KudosCount = SafeParseCsvIntField(parts, 7),
                                                BookmarksCount = SafeParseCsvIntField(parts, 8),
                                                Rating = SafeParseCsvField(parts, 9),
                                                Warnings = SafeParseCsvField(parts, 10),
                                                Category = SafeParseCsvField(parts, 11),
                                                LastSeenPage = SafeParseCsvIntField(parts, 12),
                                                DateAdded = DateTime.Parse(parts[13]),
                                                DateUpdated = DateTime.Parse(parts[14]),
                                            };
                                        });
            if (!parsedContent.Any())
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

        private static string SafeParseCsvField(string[] parts, int index)
        {
            if (parts.Length == index)
                return string.Empty;
            return parts[index];
        }

        private static int SafeParseCsvIntField(string[] parts, int index)
        {
            if (parts.Length == index)
                return 0;

            var success = int.TryParse(parts[index], out var result);
            if (!success)
                return 0;

            return result;
        }
    }
}
