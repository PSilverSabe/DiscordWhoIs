using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IAuthorRepository : IRepository<Author>
{
    Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName);

    Task<Author?> GetByIdAsync(int id);

    Task<Author?> GetByDiscordIdAsync(ulong discordId);

    Task<IReadOnlyList<Author>> GetAllByNameAsync(string authorName);

    Task<bool> DiscordIdAlreadyExists(ulong discordId);

    Task<bool> UpdateDiscordUsernameAsync(int authorId, string discordUsername, ulong discordId, bool removeDiscordIdBeforeReapply = false);

    Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description);

    Task<bool> UpdateAuthorDescriptionAsync(ulong discordId, string description);

    Task<bool> UpdateAuthorDescriptionAsync(Author author, string description);

    Task<bool> UpdateAuthorAsync(Author author);
}
