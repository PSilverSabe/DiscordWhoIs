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

// Repository for importing and querying fanfics + authors.
// Uses an IDbContextFactory so callers can create short-lived contexts.
public partial class FanficRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<FanficRepository> logger)
    : RepositoryBase<BotDbContext, FanficRepository>(dbContextFactory, logger), IFanficRepository
{
    // JSON options for deserializing the incoming export file (snake_case keys).
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Regex used to parse "alias (canonical)" author strings from AO3 exports.
    private static readonly Regex s_ao3AuthorRegex = Ao3CanonicalAuthorRegex();

    // Simple query helpers -------------------------------------------------

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

        // Return fanfics where the author is either the primary AO3 profile name
        // or matches one of the stored aliases.
        string normalized = name.Trim().ToLowerInvariant();
        return await context.Authors
            .Where(a =>
                a.Ao3ProfileName == normalized ||
                a.Aliases.Any(al => al.AliasUserName == normalized))
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

    public async Task<Fanfic?> GetByLinkAsync(string link)
    {
        await using BotDbContext context = _dbContextFactory.CreateDbContext();
        return await context.Fanfics
            .Include(f => f.Authors)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Link == link);
    }

    // High level import entry point ----------------------------------------

    public async Task<bool> ImportFromJsonAsync(string jsonFileName)
    {
        // Parse the JSON file into DTOs first.
        List<FanficJsonImport> parsedContent = await LoadJsonAsync(jsonFileName);
        if (parsedContent.Count == 0)
        {
            return false;
        }

        // Create a single DbContext for the duration of the import so EF can track
        // added entities (authors, aliases, fanfics) and maintain relationships.
        await using BotDbContext context = _dbContextFactory.CreateDbContext();

        try
        {
            // Import authors (creates new Author entities if needed and attaches aliases).
            Dictionary<string, Author> authorsByCanonical = await ImportAuthorsAsync(context, parsedContent);

            // Import fanfics linking to the authors created/loaded above.
            await ImportFanficsAsync(context, parsedContent, authorsByCanonical);

            _logger.LogInformation("Fanfic import completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            // Log DB connection string to help diagnose issues during import.
            _logger.LogError(ex, "Database error during fanfic import. Connection: {ConnectionString}", context.Database.GetConnectionString());
            throw;
        }
    }

    // JSON loading/parsing --------------------------------------------------

    private async Task<List<FanficJsonImport>> LoadJsonAsync(string jsonFileName)
    {
        if (!File.Exists(jsonFileName))
        {
            _logger.LogWarning("JSON file '{JsonFileName}' does not exist", jsonFileName);
            return [];
        }

        try
        {
            // Deserialize directly from the file stream to avoid reading whole file into memory.
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

    // Author import and alias resolution -----------------------------------

    private async Task<Dictionary<string, Author>> ImportAuthorsAsync(BotDbContext context, List<FanficJsonImport> parsedContent)
    {
        _logger.LogInformation("Processing authors.");

        // parsedAuthors is a list of tuples: (CanonicalName, OptionalAlias)
        // Canonical is the AO3 profile; Alias is the displayed alias (if present).
        var parsedAuthors = parsedContent
            .SelectMany(f => f.Authors)
            .Select(ParseAuthor)
            .Distinct()
            .ToList();

        // We will load any existing authors from the DB matching the canonical names
        // so we can update them or attach aliases to them.
        var canonicalNames = parsedAuthors
            .Select(a => a.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, Author> authors = await context.Authors
            .Include(a => a.Aliases)
            .Where(a => canonicalNames.Contains(a.Ao3ProfileName))
            .ToDictionaryAsync(a => a.Ao3ProfileName, StringComparer.OrdinalIgnoreCase);

        // PRELOAD alias names already present in the database so we don't attempt to insert duplicates.
        // Use a case-insensitive comparer to match DB collation/intent.
        var existingAliasNames = new HashSet<string>(
            await context.Aliases.Select(a => a.AliasUserName).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        // Also track aliases that are already added to the current DbContext change tracker
        // (prevents adding the same alias twice during a single import run).
        var trackedAliasNames = context.ChangeTracker.Entries<Alias>()
            .Where(e => e.State != EntityState.Detached)
            .Select(e => e.Entity.AliasUserName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

                // Add a new tracked Author entity to the context.
                context.Authors.Add(author);
                authors[canonical] = author;
            }

            if (alias != null)
            {
                // Check three places before creating a new Alias:
                // 1) already present on this Author instance,
                // 2) present in the DB (existingAliasNames),
                // 3) already created/tracked during this import (trackedAliasNames).
                bool aliasAlreadyOnAuthor = author.Aliases.Any(a => string.Equals(a.AliasUserName, alias, StringComparison.OrdinalIgnoreCase));
                bool aliasAlreadyKnown = existingAliasNames.Contains(alias) || trackedAliasNames.Contains(alias);

                if (!aliasAlreadyOnAuthor && !aliasAlreadyKnown)
                {
                    // Create and attach the alias to this author.
                    // Also add to trackedAliasNames so subsequent iterations don't create the same alias again.
                    var newAlias = new Alias(alias, author.AuthorId);
                    author.Aliases.Add(newAlias);
                    trackedAliasNames.Add(alias);
                }
                // If aliasAlreadyKnown is true, we skip adding a duplicate here.
                // ResolveAliasConflictsAsync will handle reassigning DB aliases to the correct canonical author.
            }
        }

        // Resolve alias conflicts where an alias string maps to an Author that is
        // different than the canonical author parsed for this import. This may
        // reassign existing Alias rows to the canonical Author detected in the import.
        //
        // Important: authors may include newly created (unsaved) Author entities,
        // so ResolveAliasConflictsAsync needs to consult the ChangeTracker as well
        // as the database to find canonical authors.
        await ResolveAliasConflictsAsync(context, parsedAuthors);

        // Persist authors + alias changes before importing fanfics so that AuthorId
        // values exist for new authors and foreign keys are stable.
        await SaveChangesAsync(context);

        return authors;
    }

    private async Task ResolveAliasConflictsAsync(BotDbContext context, List<(string Canonical, string? Alias)> parsedAuthors)
    {
        // Build the set of alias strings that appear in the import.
        var aliasNames = parsedAuthors
            .Where(p => p.Alias != null)
            .Select(p => p.Alias!)
            .Distinct()
            .ToList();

        // Load existing Alias rows from the DB that match any incoming alias string.
        // Include the related Author navigation property so we know the current assignment.
        List<Alias> conflicts = await context.Aliases
            .Include(a => a.Author)
            .Where(a => aliasNames.Contains(a.AliasUserName))
            .ToListAsync();

        // There may also be Alias objects that were created during this import and are
        // only tracked in-memory (not yet saved). Include those tracked entries as well.
        IEnumerable<Alias> trackedConflicts = context.ChangeTracker.Entries<Alias>()
            .Where(e => e.State != EntityState.Detached && aliasNames.Contains(e.Entity.AliasUserName))
            .Select(e => e.Entity);

        // Merge the DB-loaded conflicts with any tracked ones, avoiding duplicates.
        conflicts.AddRange(trackedConflicts.Except(conflicts));

        foreach (Alias? conflict in conflicts)
        {
            // Find the incoming parsed tuple for this alias. Use a case-insensitive match
            // since AO3 names may differ in case but should be treated equivalently here.
            (string Canonical, string? Alias) incoming = parsedAuthors.First(p => string.Equals(p.Alias, conflict.AliasUserName, StringComparison.OrdinalIgnoreCase));

            // If the alias already points to the same canonical author, nothing to do.
            if (conflict.Author != null &&
                string.Equals(conflict.Author.Ao3ProfileName, incoming.Canonical, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Prefer finding the canonical Author among change-tracked Author entities
            // (these include newly created authors that haven't been saved yet).
            Author? canonicalAuthor = context.ChangeTracker.Entries<Author>()
                .Select(e => e.Entity)
                .FirstOrDefault(a => string.Equals(a.Ao3ProfileName, incoming.Canonical, StringComparison.OrdinalIgnoreCase));

            if (canonicalAuthor == null)
            {
                // If not found in the tracker, fall back to querying the DB.
                // Use FirstOrDefaultAsync to avoid InvalidOperationException when no rows exist.
                canonicalAuthor = await context.Authors
                    .FirstOrDefaultAsync(a => a.Ao3ProfileName == incoming.Canonical);
            }

            if (canonicalAuthor == null)
            {
                // If we still can't find the canonical author, log and skip.
                // This can happen if the parsed canonical name is malformed or missing.
                _logger.LogWarning(
                    "Canonical author '{Canonical}' not found for alias '{Alias}'. Skipping reassignment.",
                    incoming.Canonical,
                    conflict.AliasUserName);
                continue;
            }

            // Reassign the alias to point to the canonical author.
            // Set both the navigation property and FK so EF's change tracker keeps
            // everything consistent for both new/tracked entities and persisted rows.
            string oldName = conflict.Author?.Ao3ProfileName ?? "<unknown>";
            conflict.Author = canonicalAuthor;
            conflict.AuthorId = canonicalAuthor.AuthorId;

            _logger.LogWarning(
                "Alias '{Alias}' reassigned from '{Old}' to '{New}'",
                conflict.AliasUserName,
                oldName,
                canonicalAuthor.Ao3ProfileName);
        }
    }

    // Fanfic import --------------------------------------------------------

    private async Task ImportFanficsAsync(
        BotDbContext context,
        List<FanficJsonImport> parsedContent,
        Dictionary<string, Author> authorsByCanonical)
    {
        _logger.LogInformation("Processing fanfics.");

        // Map the parsed import DTOs into Fanfic DB entities, keyed by their Link
        // so we can detect updates vs. new fanfics.
        var incomingByLink = parsedContent
            .Select(f => MapJsonFanficToDatabaseFanfic(f, authorsByCanonical))
            .ToDictionary(f => f.Link, StringComparer.OrdinalIgnoreCase);

        var incomingLinks = incomingByLink.Keys.ToList();

        // Load existing fanfics that match any incoming link and include their authors
        // for authorship reconciliation.
        List<Fanfic> existingFanfics = await context.Fanfics
            .Include(f => f.Authors)
            .Where(f => incomingLinks.Contains(f.Link))
            .ToListAsync();

        // Update existing fanfics' scalar properties and reconcile authorship.
        foreach (Fanfic? existing in existingFanfics)
        {
            Fanfic incoming = incomingByLink[existing.Link];

            UpdateFanficScalars(existing, incoming);

            // Reconcile authorship (add/remove relations) using helper logic.
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

        // Determine which incoming fanfics are new and add them.
        var existingLinks = existingFanfics.Select(f => f.Link).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFanfics = incomingByLink
            .Where(kvp => !existingLinks.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        context.Fanfics.AddRange(newFanfics);

        // Save all fanfic additions/updates and any pending FK fixes from author import.
        await SaveChangesAsync(context);
    }

    // Helper to copy scalar properties from an incoming mapped Fanfic to an existing DB Fanfic.
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

    // Map an import DTO to a Fanfic DB model and attach Author entities by canonical name.
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

        // Ensure we don't add duplicate authors to the Fanfic.Authors collection
        // (authorsByCanonical maps canonical name -> Author entity).
        var seenAuthorIds = new HashSet<int>();

        foreach (string rawAuthor in fanficJsonImport.Authors)
        {
            (string canonical, _) = ParseAuthor(rawAuthor);

            if (!authorsByCanonical.TryGetValue(canonical, out Author? author))
            {
                // If an author wasn't created/loaded earlier (should be rare), skip gracefully.
                continue;
            }

            if (seenAuthorIds.Add(author.AuthorId))
            {
                fanfic.Authors.Add(author);
            }
        }

        return fanfic;
    }

    // Parse author strings like "DisplayName (CanonicalProfileName)".
    // Returns (canonical, alias) where alias may be null if no parentheses present.
    private static (string Canonical, string? Alias) ParseAuthor(string raw)
    {
        Match match = s_ao3AuthorRegex.Match(raw);

        if (!match.Success)
        {
            // No alias/canonical syntax found; treat the whole string as canonical.
            return (raw.Trim(), null);
        }

        // Regex groups named "canonical" and "alias" make the intent explicit.
        return (
            match.Groups["canonical"].Value.Trim(),
            match.Groups["alias"].Value.Trim()
        );
    }

    // Regex factory - matches "alias (canonical)" with named capture groups.
    [GeneratedRegex(@"^(?<alias>.*?)\s*\(\s*(?<canonical>[^()]+)\s*\)$", RegexOptions.Compiled)]
    private static partial Regex Ao3CanonicalAuthorRegex();
}
