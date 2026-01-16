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

        return await context.Authors
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Author>> GetAllByNameAsync(string name)
    {
        string normalized = Normalize(name);

        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .Where(a =>
                a.Ao3ProfileName == normalized ||
                a.FanficNetProfileName == normalized ||
                a.DiscordUserName == normalized ||
                a.Aliases.Any(alias =>
                    alias.AliasUserName == normalized))
            .ToListAsync();
    }

    public async Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName)
    {
        string normalized = Normalize(ao3ProfileName);

        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .FirstOrDefaultAsync(a =>
                a.Ao3ProfileName == normalized ||
                a.Aliases.Any(alias =>
                    alias.AliasUserName == normalized));
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AuthorId == id);
    }

    public async Task<Author?> GetByDiscordIdAsync(ulong discordId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DiscordId == discordId);
    }

    public async Task<bool> DiscordIdAlreadyExists(ulong discordId)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Authors
            .AsNoTracking()
            .AnyAsync(a => a.DiscordId == discordId);
    }

    public async Task<bool> UpdateAuthorAsync(Author author)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        context.Authors.Attach(author);
        context.Entry(author).State = EntityState.Modified;

        await SaveChangesAsync(context);
        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description)
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == authorId);

        if (author is null)
        {
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

        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.DiscordId == discordId);

        if (author is null)
        {
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

        Author? dbAuthor = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == author.AuthorId);

        if (dbAuthor is null)
        {
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
