using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IAuthorRepository : IRepository<Author>
{
    Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName);

    Task<Author?> GetByIdAsync(int id);

    Task<Author?> GetByDiscordIdAsync(ulong discordId);

    Task<IReadOnlyList<Author>> GetAllByNameAsync(string authorName);

    Task<bool> UpdateDiscordUsernameAsync(int authorId, string discordUsername, ulong discordId);

    Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description);

    Task<bool> UpdateAuthorAsync(Author author);
}
