using System.Text.Json;
using System.Text.RegularExpressions;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository;
using DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository.Models;
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
        _logger.LogInformation("Getting all fanfics.");
        return await context.Fanfics
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string name)
    {
        await using BotDbContext context = _dbContextFactory.CreateDbContext();
        _logger.LogInformation("Getting all Authors by given author: {Author}", name);
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
        _logger.LogInformation("Getting all Fanfics by given id: {Id}", id);
        return await context.Fanfics
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FanficId == id);
    }

    public async Task<Fanfic?> GetByTitleAsync(string title)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();
        _logger.LogInformation("Getting all Fanfics by given title: {title}", title);
        return await context.Fanfics
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Title == title);
    }

    public async Task<bool> ImportFromJsonAsync(string jsonFileName)
    {
        List<FanficJsonImport> parsedContent = await LoadJsonAsync(jsonFileName);
        if (parsedContent.Count == 0)
        {
            return false;
        }

        await using BotDbContext context = _dbContextFactory.CreateDbContext();

        try
        {
            Dictionary<string, Author> authorsByCanonical = await ImportAuthorsAsync(context, parsedContent);
            await ImportFanficsAsync(context, parsedContent, authorsByCanonical);
            _logger.LogInformation("Fanfic import completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error during fanfic import. Connection: {ConnectionString}", context.Database.GetConnectionString());
            throw;
        }
    }

    private async Task<List<FanficJsonImport>> LoadJsonAsync(string jsonFileName)
    {
        if (!File.Exists(jsonFileName))
        {
            _logger.LogWarning("JSON file '{JsonFileName}' does not exist", jsonFileName);
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(jsonFileName);
            List<FanficJsonImport>? parsed = await JsonSerializer.DeserializeAsync<List<FanficJsonImport>>(stream, _options);

            if (parsed == null || parsed.Count == 0)
            {
                _logger.LogWarning("No fanfic data found in JSON file '{JsonFileName}'", jsonFileName);
                return [];
            }

            _logger.LogInformation("Importing {Count} fanfics from JSON file '{JsonFileName}'", parsed.Count, jsonFileName);

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse JSON file '{JsonFileName}'", jsonFileName);
            throw;
        }
    }

    private async Task<Dictionary<string, Author>> ImportAuthorsAsync(BotDbContext context, List<FanficJsonImport> parsedContent)
    {
        _logger.LogInformation("Processing authors.");

        var parsedAuthors = parsedContent
            .SelectMany(f => f.Authors)
            .Select(ParseAuthor)
            .Distinct()
            .ToList();

        var canonicalNames = parsedAuthors
            .Select(a => a.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, Author> authors = await context.Authors
            .Include(a => a.Aliases)
            .Where(a => canonicalNames.Contains(a.Ao3ProfileName))
            .ToDictionaryAsync(a => a.Ao3ProfileName, StringComparer.OrdinalIgnoreCase);

        foreach ((string canonical, string? alias) in parsedAuthors)
        {
            if (!authors.TryGetValue(canonical, out Author? author))
            {
                author = new Author
                {
                    Ao3ProfileName = canonical,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow
                };

                context.Authors.Add(author);
                authors[canonical] = author;
            }

            if (alias != null && !author.Aliases.Any(a => a.AliasUserName == alias))
            {
                author.Aliases.Add(new Alias(alias, author.AuthorId));
            }
        }

        await ResolveAliasConflictsAsync(context, parsedAuthors);
        await SaveChangesAsync(context);

        return authors;
    }

    private async Task ResolveAliasConflictsAsync(BotDbContext context, List<(string Canonical, string? Alias)> parsedAuthors)
    {
        var aliasNames = parsedAuthors
            .Where(p => p.Alias != null)
            .Select(p => p.Alias!)
            .Distinct()
            .ToList();

        List<Alias> conflicts = await context.Aliases
            .Include(a => a.Author)
            .Where(a => aliasNames.Contains(a.AliasUserName))
            .ToListAsync();

        foreach (Alias? conflict in conflicts)
        {
            (string Canonical, string? Alias) incoming = parsedAuthors.First(p => p.Alias == conflict.AliasUserName);

            if (conflict.Author.Ao3ProfileName == incoming.Canonical)
            {
                continue;
            }

            Author canonicalAuthor = await context.Authors
                .SingleAsync(a => a.Ao3ProfileName == incoming.Canonical);

            conflict.AuthorId = canonicalAuthor.AuthorId;

            _logger.LogWarning(
                "Alias '{Alias}' reassigned from '{Old}' to '{New}'",
                conflict.AliasUserName,
                conflict.Author.Ao3ProfileName,
                canonicalAuthor.Ao3ProfileName);
        }
    }

    private async Task ImportFanficsAsync(
        BotDbContext context,
        List<FanficJsonImport> parsedContent,
        Dictionary<string, Author> authorsByCanonical)
    {
        _logger.LogInformation("Processing fanfics.");

        var incomingByLink = parsedContent
            .Select(f => MapJsonFanficToDatabaseFanfic(f, authorsByCanonical))
            .ToDictionary(f => f.Link, StringComparer.OrdinalIgnoreCase);

        var incomingLinks = incomingByLink.Keys.ToList();

        List<Fanfic> existingFanfics = await context.Fanfics
            .Include(f => f.Authors)
            .Where(f => incomingLinks.Contains(f.Link))
            .ToListAsync();

        foreach (Fanfic? existing in existingFanfics)
        {
            Fanfic incoming = incomingByLink[existing.Link];

            UpdateFanficScalars(existing, incoming);

            AuthorshipDelta delta = FanficAuthorshipReconciler.Reconcile(existing, incoming);
            if (delta.HasChanges)
            {
                _logger.LogInformation(
                    "Updated authorship for '{Title}'. Added: [{Added}] Removed: [{Removed}]",
                    existing.Title,
                    string.Join(", ", delta.Added.Select(a => a.Ao3ProfileName)),
                    string.Join(", ", delta.Removed.Select(a => a.Ao3ProfileName)));
            }
        }

        var existingLinks = existingFanfics.Select(f => f.Link).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFanfics = incomingByLink
            .Where(kvp => !existingLinks.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        context.Fanfics.AddRange(newFanfics);

        await SaveChangesAsync(context);
    }

    private static void UpdateFanficScalars(Fanfic target, Fanfic source)
    {
        target.Title = source.Title;
        target.Summary = source.Summary;
        target.WordCount = source.WordCount;
        target.HitCount = source.HitCount;
        target.CommentCount = source.CommentCount;
        target.KudosCount = source.KudosCount;
        target.BookmarksCount = source.BookmarksCount;
        target.ChapterCount = source.ChapterCount;
        target.FicLastUpdated = source.FicLastUpdated;
        target.DateUpdated = DateTime.UtcNow;
    }

    private static Fanfic MapJsonFanficToDatabaseFanfic(
        FanficJsonImport fanficJsonImport,
        Dictionary<string, Author> authorsByCanonical)
    {
        var fanfic = new Fanfic
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

        // Enforce set semantics
        var seenAuthorIds = new HashSet<int>();

        foreach (string rawAuthor in fanficJsonImport.Authors)
        {
            (string canonical, _) = ParseAuthor(rawAuthor);

            if (!authorsByCanonical.TryGetValue(canonical, out Author? author))
            {
                continue;
            }

            if (seenAuthorIds.Add(author.AuthorId))
            {
                fanfic.Authors.Add(author);
            }
        }

        return fanfic;
    }

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
