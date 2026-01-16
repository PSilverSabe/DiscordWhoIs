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
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        List<Author> authors = await context.Authors.AsNoTracking().ToListAsync();
        return authors;
    }

    public async Task<IReadOnlyList<Author>> GetAllByNameAsync(string ao3ProfileName)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        string lowerName = ao3ProfileName.ToLower();

        List<Author> authors = [.. context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .Where(a =>
                a.Ao3ProfileName.ToLower() == lowerName ||
                (a.FanficNetProfileName != null && a.FanficNetProfileName.ToLower() == lowerName) ||
                (a.DiscordUsername != null && a.DiscordUsername.ToLower() == lowerName) ||
                a.Aliases.Any(alias => alias.AliasUserName.ToLower() == lowerName)
            )];



        return authors;
    }

    public async Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName)
    {
        IReadOnlyList<Author> authors = await GetAllByNameAsync(ao3ProfileName);

        if (authors.Count == 0)
        {
            return default;
        }

        return authors[0];
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        Author? author = await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AuthorId == id);

        return author;
    }

    public async Task<Author?> GetByDiscordIdAsync(ulong discordId)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        Author? author = await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DiscordId == discordId);

        return author;
    }

    public async Task<bool> UpdateAuthorAsync(Author author)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        context.Entry(author).CurrentValues.SetValues(author);
        await SaveChangesAsync(context);

        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();
        Author? author = await context.Authors.FirstOrDefaultAsync(a => a.AuthorId == authorId);

        if (author == null)
        {
            return false;
        }

        author.Description = description;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);


        return true;
    }

    public async Task<bool> UpdateDiscordUsernameAsync(int authorId, string discordUsername, ulong discordId, bool removeDiscordIdBeforeReapply = false)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        if (removeDiscordIdBeforeReapply)
        {
            List<Author> authors = await context.Authors.Where(a => a.DiscordId == discordId).ToListAsync();
            foreach (Author? a in authors)
            {
                a.DiscordId = null;
                a.DiscordUsername = null;
                a.LastUpdatedAt = DateTime.UtcNow;
            }
            await SaveChangesAsync(context);
        }

        Author? author = context.Authors.FirstOrDefault(a => a.AuthorId == authorId);
        if (author == null)
        {
            return false;
        }

        author.DiscordUsername = discordUsername;
        author.DiscordId = discordId;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);

        return true;
    }

    public async Task<bool> DiscordIdAlreadyExists(ulong discordId)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        bool exists = await context.Authors
            .AsNoTracking()
            .AnyAsync(a => a.DiscordId == discordId);

        return exists;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(ulong discordId, string description)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        Author? author = context.Authors.FirstOrDefault(a => a.DiscordId == discordId);

        if (author == null)
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
        using BotDbContext context = _dbContextFactory.CreateDbContext();
        Author? dbAuthor = context.Authors.FirstOrDefault(a => a.AuthorId == author.AuthorId);

        if (dbAuthor == null)
        {
            return false;
        }

        dbAuthor.Description = description;
        dbAuthor.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);

        return true;
    }
}
