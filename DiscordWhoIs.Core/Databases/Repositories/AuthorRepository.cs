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

        List<Author> authors = context.Authors
            .AsNoTracking()
            .ToList();

        context.Dispose();

        return authors;
    }

    public async Task<IReadOnlyList<Author>> GetAllByNameAsync(string authorName)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        string lowerAuthor = authorName.ToLower();

        List<Author> authors = context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .Where(a =>
                a.Ao3ProfileName.ToLower() == lowerAuthor ||
                a.FanficNetProfileName == authorName ||
                a.DiscordUsername == authorName ||
                a.Aliases.Any(alias => alias.AliasUserName.ToLower() == lowerAuthor)
            )
            .ToList();

        context.Dispose();

        return authors;
    }

    public async Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        string lowerName = ao3ProfileName.ToLower();

        // Attempt to find canonical match first
        Author? author = await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .FirstOrDefaultAsync(a => a.Ao3ProfileName.ToLower() == lowerName ||
                                      a.FanficNetProfileName == ao3ProfileName ||
                                      a.DiscordUsername == ao3ProfileName);

        if (author != null)
        {
            return author;
        }

        // If no canonical match, check aliases
        author = await context.Authors
            .AsNoTracking()
            .Include(a => a.Aliases)
            .FirstOrDefaultAsync(a => a.Aliases.Any(alias => alias.AliasUserName.ToLower() == lowerName));

        context.Dispose();

        return author;
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        Author? author = await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AuthorId == id);

        context.Dispose();

        return author;
    }

    public async Task<Author?> GetByDiscordIdAsync(ulong discordId)
    {
        using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        Author? author = await context.Authors
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DiscordId == discordId);

        context.Dispose();

        return author;
    }

    public async Task<bool> UpdateAuthorAsync(Author author)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        context.Entry(author).CurrentValues.SetValues(author);

        await SaveChangesAsync(context);

        context.Dispose();

        return true;
    }

    public async Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        Author? author = context.Authors.FirstOrDefault(a => a.AuthorId == authorId);

        if (author == null)
        {
            return false;
        }

        author.Description = description;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);

        context.Dispose();

        return true;
    }

    public async Task<bool> UpdateDiscordUsernameAsync(int authorId, string discordUsername, ulong discordId)
    {
        using BotDbContext context = _dbContextFactory.CreateDbContext();

        Author? author = context.Authors.FirstOrDefault(a => a.AuthorId == authorId);
        if (author == null)
        {
            return false;
        }

        author.DiscordUsername = discordUsername;
        author.DiscordId = discordId;
        author.LastUpdatedAt = DateTime.UtcNow;

        await SaveChangesAsync(context);

        context.Dispose();

        return true;
    }
}
