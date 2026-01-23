using System.Text.Json;
using System.Text.RegularExpressions;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

public partial class FanficRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<FanficRepository> logger)
    : RepositoryBase<BotDbContext, FanficRepository>(dbContextFactory, logger), IFanficRepository
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly Regex s_ao3AuthorRegex = Ao3CanonicalAuthorRegex();

    public async Task<IReadOnlyList<Fanfic>> GetAllAsync()
    {
        await using BotDbContext context = _dbContextFactory.CreateDbContext();
        return await context.Fanfics
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string name)
    {
        await using BotDbContext context = _dbContextFactory.CreateDbContext();

        return await context.Authors
            .Where(a =>
                a.Ao3ProfileName == name ||
                a.Aliases.Any(al => al.AliasUserName == name))
            .SelectMany(a => a.Fanfics)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Fanfic?> GetByIdAsync(int id)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();
        return await context.Fanfics
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FanficId == id);
    }

    public async Task<Fanfic?> GetByTitleAsync(string title)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();
        return await context.Fanfics
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Title == title);
    }

    public async Task<bool> ImportFromJsonAsync(string jsonFileName)
    {
        bool jsonFileExists = File.Exists(jsonFileName);
        if (!jsonFileExists)
        {
            _logger.LogWarning("JSON file '{JsonFileName}' does not exist", jsonFileName);
            return false;
        }

        List<FanficJsonImport>? parsedContent = null;

        try
        {
            using FileStream stream = File.OpenRead(jsonFileName);
            parsedContent = await JsonSerializer.DeserializeAsync<List<FanficJsonImport>>(stream, _options);
            stream.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse JSON file '{JsonFileName}'", jsonFileName);
            throw;
        }

        if (parsedContent == null || parsedContent.Count == 0)
        {
            _logger.LogWarning("No fanfic data found in JSON file '{JsonFileName}'", jsonFileName);
            return false;
        }

        _logger.LogInformation("Importing {Count} fanfics from JSON file '{JsonFileName}'", parsedContent.Count, jsonFileName);

        using BotDbContext context = _dbContextFactory.CreateDbContext();

        _logger.LogInformation("Processing authors.");
        var parsedAuthors = parsedContent
            .SelectMany(f => f.Authors)
            .Select(ParseAuthor)
            .ToList();


        _logger.LogInformation("Ensuring authors and aliases exist in database.");
        var canonicalNames = parsedAuthors
            .Select(a => a.Canonical)
            .Distinct()
            .ToList();

        _logger.LogInformation("Getting existing authors from database and are in import file.");
        var existingAuthors = context.Authors
            .Include(a => a.Aliases)
            .Where(a => canonicalNames.Contains(a.Ao3ProfileName))
            .ToDictionary(a => a.Ao3ProfileName);

        _logger.LogInformation("Adding new and updating existing authors.");
        foreach ((string? canonical, string? alias) in parsedAuthors)
        {
            if (!existingAuthors.TryGetValue(canonical, out Author? author))
            {
                author = new Author
                {
                    Ao3ProfileName = canonical,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow
                };

                context.Authors.Add(author);
                existingAuthors[canonical] = author;
            }

            if (alias != null && !author.Aliases.Any(a => a.AliasUserName == alias))
            {
                author.Aliases.Add(new Alias(alias, author.AuthorId));
            }
        }

        _logger.LogInformation("Gather authors with Aliases.");
        var aliasNames = parsedAuthors
            .Where(p => p.Alias != null)
            .Select(p => p.Alias!)
            .Distinct()
            .ToList();

        _logger.LogInformation("Checking for conflicting aliases in database.");
        List<Alias> conflictingAliases = await context.Aliases
            .Include(a => a.Author)
            .Where(a => aliasNames.Contains(a.AliasUserName))
            .ToListAsync();

        _logger.LogInformation("Resolving {Count} conflicting aliases.", conflictingAliases.Count);
        foreach (Alias? conflict in conflictingAliases)
        {
            (string Canonical, string? Alias) incoming = parsedAuthors
                .First(p => p.Alias == conflict.AliasUserName);

            if (conflict.Author.Ao3ProfileName == incoming.Canonical)
            {
                continue; // already correct
            }

            Author canonicalAuthor = await context.Authors
                .SingleAsync(a => a.Ao3ProfileName == incoming.Canonical);

            // Prefer canonical ownership
            conflict.AuthorId = canonicalAuthor.AuthorId;

            _logger.LogWarning(
                "Alias '{Alias}' reassigned from '{Old}' to '{New}'",
                conflict.AliasUserName,
                conflict.Author.Ao3ProfileName,
                canonicalAuthor.Ao3ProfileName);
        }

        _logger.LogInformation("Saving authors and aliases to database.");
        await SaveChangesAsync(context);

        try
        {
            _logger.LogInformation("Processing fanfics.");
            var incomingByLink = parsedContent
                .Select(MapJsonFanficToDatabaseFanfic)
                .ToDictionary(f => f.Link, StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Checking for existing fanfics in database.");
            var incomingLinks = incomingByLink.Keys.ToList();
            var existingLinks = context.Fanfics
                .Where(f => incomingLinks.Contains(f.Link))
                .Select(f => f.Link)
                .ToList();

            _logger.LogInformation("Updating {Count} existing fanfics.", existingLinks.Count);
            foreach (string? link in existingLinks)
            {
                Fanfic incoming = incomingByLink[link];

                int returnVal = await context.Fanfics
                                        .Where(f => f.Link == link)
                                        .ExecuteUpdateAsync(setters => setters
                                            .SetProperty(f => f.Title, incoming.Title)
                                            .SetProperty(f => f.Summary, incoming.Summary)
                                            .SetProperty(f => f.WordCount, incoming.WordCount)
                                            .SetProperty(f => f.HitCount, incoming.HitCount)
                                            .SetProperty(f => f.CommentCount, incoming.CommentCount)
                                            .SetProperty(f => f.KudosCount, incoming.KudosCount)
                                            .SetProperty(f => f.BookmarksCount, incoming.BookmarksCount)
                                            .SetProperty(f => f.ChapterCount, incoming.ChapterCount)
                                            .SetProperty(f => f.FicLastUpdated, incoming.FicLastUpdated)
                                            .SetProperty(f => f.DateUpdated, DateTime.UtcNow)
                                        );
                _logger.LogInformation("Updated {ReturnVal} record(s) for fanfic link '{Link}'", returnVal, link);
            }

            _logger.LogInformation("Adding new fanfics.");
            var newFanfics = incomingByLink
                    .Where(kvp => !existingLinks.Contains(kvp.Key))
                    .Select(kvp => kvp.Value)
                    .ToList();
            _logger.LogInformation("Adding {Count} new fanfics to database.", newFanfics.Count);
            context.Fanfics.AddRange(newFanfics);

            await SaveChangesAsync(context);
            _logger.LogInformation("Fanfic import completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
            Console.WriteLine(ex);
        }

        return true;
    }

    private static Fanfic MapJsonFanficToDatabaseFanfic(FanficJsonImport fanficJsonImport) => new()
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

    private static (string Canonical, string? Alias) ParseAuthor(string raw)
    {
        Match match = s_ao3AuthorRegex.Match(raw);

        if (!match.Success)
        {
            return (raw.Trim(), null);
        }

        return (
            match.Groups["canonical"].Value.Trim(),
            match.Groups["alias"].Value.Trim()
        );
    }

    [GeneratedRegex(@"^(?<alias>.*?)\s*\(\s*(?<canonical>[^()]+)\s*\)$", RegexOptions.Compiled)]
    private static partial Regex Ao3CanonicalAuthorRegex();
}
