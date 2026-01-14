using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public class FanficRepository : RepositoryBase<BotDbContext, FanficRepository>, IFanficRepository
    {
        private readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public FanficRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<FanficRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        public Task<IReadOnlyList<Fanfic>> GetAllAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            IReadOnlyList<Fanfic> results = [.. context.Fanfics.AsNoTracking()];
            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string author)
        {
            IReadOnlyList<Fanfic> results = [];
            using var context = _dbContextFactory.CreateDbContext();
            var lowerAuthor = author.ToLower();
            var dbAuthor = context.Authors
                                    .Where(a => a.Ao3ProfileName.ToLower() == lowerAuthor || a.Ao3ProfileName.ToLower().Contains(lowerAuthor))
                                    .Include(x => x.Fanfics)
                                    .FirstOrDefault();

            if (dbAuthor != null) 
            {
                results = [.. dbAuthor.Fanfics];
                return Task.FromResult(results);
            }

            return Task.FromResult(results);
        }

        public Task<Fanfic?> GetByIdAsync(int id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return Task.FromResult(context.Fanfics.AsNoTracking().FirstOrDefault(f => f.FanficId == id));
        }

        public Task<Fanfic?> GetByTitleAsync(string title)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return Task.FromResult(context.Fanfics.AsNoTracking().FirstOrDefault(f => f.Title == title));
        }

        public Task<bool> ImportFromJsonAsync(string jsonFileName)
        {
            var jsonFileExists = File.Exists(jsonFileName);
            if (!jsonFileExists)
            {
                return Task.FromResult(false);
            }


            using FileStream stream = File.OpenRead(jsonFileName);
            var parsedContent = JsonSerializer.Deserialize<List<FanficJsonImport>>(stream, options);
            stream.Dispose();

            if (parsedContent == null || parsedContent.Count == 0)
            {
                return Task.FromResult(false);
            }

            using var context = _dbContextFactory.CreateDbContext();

            try
            {
                var existingAuthors = context.Authors.AsNoTracking().ToDictionary(a => a.Ao3ProfileName, StringComparer.OrdinalIgnoreCase);
                var fileAuthors = parsedContent
                    .SelectMany(f => f.Authors)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var authorName in fileAuthors)
                {
                    // Add any missing authors
                    // Differs from Fanfic Import because here we only have profile names to go on
                    // TODO: Handle Fanfic.net authors too
                    if (!existingAuthors.TryGetValue(authorName, out var parsedAuthorName))
                    {
                        context.Add(new Author()
                        {
                            Ao3ProfileName = authorName,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow,
                            LastActiveAt = DateTime.UtcNow
                        });// New author
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }

            SaveChanges(context);

            try
            {
                // Pre-fetch existing fanfics and authors to minimize DB queries
                var existingFanfics = context.Fanfics.AsNoTracking().ToDictionary(f => f.Link, StringComparer.OrdinalIgnoreCase);
                var existingAuthors = context.Authors.AsNoTracking().ToDictionary(a => a.Ao3ProfileName, StringComparer.OrdinalIgnoreCase);

                foreach (var parsedFanfic in parsedContent)
                {
                    // Map parsedFanfic to database Fanfic model
                    var fanfic = MapJsonFanficToDatabaseFanfic(parsedFanfic);
                    // Map Recorded Authors to existing Author entities
                    // This assumes that all authors have been pre-imported, either via previous runs or the author import logic above
                    var fanficAuthors = existingAuthors
                                            .Where(a => parsedFanfic.Authors.Contains(a.Key, StringComparer.OrdinalIgnoreCase))
                                            .Select(x => x.Value.AuthorId)
                                            .Select(id => context.Authors.Find(id)!).ToList();

                    // Update LastUpdatedAt for each author
                    foreach (var author in fanficAuthors)
                    {
                        author.LastUpdatedAt = DateTime.UtcNow;
                        context.Entry(author).CurrentValues.SetValues(author);
                    }
                    fanfic.Authors = fanficAuthors;


                    // Upsert fanfic logic
                    // Check if fanfic with same Link exists, links are unique so we can use that to identify between imports
                    // If it exists, update the existing entry
                    // If not, add a new entry
                    if (existingFanfics.TryGetValue(fanfic.Link, out Fanfic? existingFanfic))
                    {
                        fanfic.FanficId = existingFanfic.FanficId; // Preserve the ID for update
                        context.Entry(existingFanfic).CurrentValues.SetValues(fanfic);
                    }
                    else
                    {
                        // New entry
                        context.Fanfics.Add(fanfic);
                    }
                }

                SaveChanges(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }
            finally
            {
                context.Dispose();
            }

            return Task.FromResult(true);
        }

        private static Fanfic MapJsonFanficToDatabaseFanfic(FanficJsonImport fanficJsonImport)
        {
            return new Fanfic()
            {
                Link = fanficJsonImport.Link,
                Title = fanficJsonImport.Title,
                Summary = fanficJsonImport.Summary,
                WordCount = fanficJsonImport.WordCount,
                HitCount = fanficJsonImport.HitCount,
                CommentCount = fanficJsonImport.CommentsCount,
                KudosCount = fanficJsonImport.KudosCount,
                BookmarksCount = fanficJsonImport.BookmarksCount,
                ChapterCount = fanficJsonImport.ChaptersCount,
                Rating = fanficJsonImport.Rating,
                Warnings = fanficJsonImport.Warnings,
                Category = fanficJsonImport.Category,
                FicLastUpdated = fanficJsonImport.FicLastUpdated,
                DateAdded = fanficJsonImport.DateAdded,
                DateUpdated = fanficJsonImport.DateUpdated
            };
        }
    }
}
