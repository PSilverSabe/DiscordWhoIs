using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

public class AuthorRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<AuthorRepository> logger)
    : RepositoryBase<BotDbContext, AuthorRepository>(dbContextFactory, logger), IAuthorRepository
{
    public async Task<IReadOnlyList<Author>> GetAllAsync()
    {
        await using BotDbContext context =
            await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching all authors with fanfics from database.");

        return await context.Authors
            .Include(a => a.Fanfics)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Author>> GetAllByNameAsync(string name)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Searching authors by name '{Name}'", name);

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .Include(a => a.Fanfics)
            .Where(a =>
                a.Ao3ProfileName == Normalize(name) ||
                a.FanficNetProfileName == Normalize(name) ||
                a.DiscordUserName == Normalize(name) ||
                a.Aliases.Any(alias =>
                    alias.AliasUserName == Normalize(name)))
            .ToListAsync();
    }

    public async Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName)
    {
        string normalized = Normalize(ao3ProfileName);

        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching author by AO3 profile name '{Ao3ProfileName}'", ao3ProfileName);

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .Include(a => a.Fanfics)
            .FirstOrDefaultAsync(a =>
                a.Ao3ProfileName == normalized ||
                a.Aliases.Any(alias =>
                    alias.AliasUserName == normalized));
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching author by id {AuthorId}", id);

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Fanfics)
            .FirstOrDefaultAsync(a => a.AuthorId == id);
    }

    public async Task<Author?> GetByDiscordIdAsync(ulong discordId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Fetching author by discord id {DiscordId}", discordId);

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Fanfics)
            .FirstOrDefaultAsync(a => a.DiscordId == discordId);
    }

    public async Task<bool> DiscordIdAlreadyExists(ulong discordId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogDebug("Checking existence of discord id {DiscordId}", discordId);

        return await context.Authors
            .AsNoTracking()
            .AnyAsync(a => a.DiscordId == discordId);
    }

    public async Task<bool> UpdateAuthorAsync(Author author)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Updating author {AuthorId}", author.AuthorId);

        context.Authors.Attach(author);
        context.Entry(author).State = EntityState.Modified;

        await SaveChangesAsync(context);
        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Updating author description for id {AuthorId}", authorId);

        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == authorId);

        if (author is null)
        {
            _logger.LogWarning("Author with id {AuthorId} not found when updating description", authorId);
            return false;
        }

        author.Description = description;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);
        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(ulong discordId, string description)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Updating author description for discord id {DiscordId}", discordId);

        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.DiscordId == discordId);

        if (author is null)
        {
            _logger.LogWarning("Author with discord id {DiscordId} not found when updating description", discordId);
            return false;
        }

        author.Description = description;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);
        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(Author author, string description)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Updating author description for author entity {AuthorId}", author.AuthorId);

        Author? dbAuthor = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == author.AuthorId);

        if (dbAuthor is null)
        {
            _logger.LogWarning("Author entity {AuthorId} not found when updating description", author.AuthorId);
            return false;
        }

        dbAuthor.Description = description;
        dbAuthor.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);
        return true;
    }

    public async Task<bool> UpdateDiscordUsernameAsync(
        int authorId,
        string discordUsername,
        ulong discordId,
        bool removeDiscordIdBeforeReapply = false)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        _logger.LogInformation("Updating discord username for author {AuthorId} to {DiscordUsername} (DiscordId: {DiscordId})", authorId, discordUsername, discordId);

        if (removeDiscordIdBeforeReapply)
        {
            List<Author> conflicts = await context.Authors
                .Where(a => a.DiscordId == discordId)
                .ToListAsync();

            foreach (Author? a in conflicts)
            {
                a.DiscordId = null;
                a.DiscordUserName = null;
                a.LastUpdatedAt = DateTime.UtcNow;
            }

            await SaveChangesAsync(context);
        }

        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == authorId);

        if (author is null)
        {
            _logger.LogWarning("Author with id {AuthorId} not found when updating discord username", authorId);
            return false;
        }

        author.DiscordUserName = discordUsername;
        author.DiscordId = discordId;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);
        return true;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
