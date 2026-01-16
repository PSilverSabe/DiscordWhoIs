using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IAuthorRepository : IRepository<Author>
{
    Task<Author?> GetByAo3ProfileNameAsync(string ao3ProfileName);

    Task<Author?> GetByIdAsync(int id);

    Task<Author?> GetByDiscordIdAsync(ulong discordId);

    Task<IReadOnlyList<Author>> GetAllByNameAsync(string authorName);

    Task<bool> DiscordIdAlreadyExists(ulong discordId);

    /// <summary>
    /// Updates the Discord username and associated Discord ID for the specified author asynchronously.
    /// </summary>
    /// <param name="authorId">The unique identifier of the author whose Discord information is to be updated.</param>
    /// <param name="discordUsername">The new Discord username to associate with the author. Cannot be null or empty.</param>
    /// <param name="discordId">The Discord user ID to associate with the author.</param>
    /// <param name="removeDiscordIdBeforeReapply">true to remove any existing Discord ID association before applying the new one; otherwise, false. Defaults to
    /// false.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the update was successful;
    /// otherwise, false.</returns>
    Task<bool> UpdateDiscordUsernameAsync(int authorId, string discordUsername, ulong discordId, bool removeDiscordIdBeforeReapply = false);

    /// <summary>
    /// Asynchronously updates the description of the specified author.
    /// </summary>
    /// <param name="authorId">The unique identifier of the author whose description is to be updated. Must be a valid, existing author ID.</param>
    /// <param name="description">The new description to assign to the author. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the author's
    /// description was successfully updated; otherwise, <see langword="false"/>.</returns>
    Task<bool> UpdateAuthorDescriptionAsync(int authorId, string description);

    /// <summary>
    /// Asynchronously updates the description for the specified author identified by their Discord user ID.
    /// </summary>
    /// <param name="discordId">The unique Discord user ID of the author whose description is to be updated.</param>
    /// <param name="description">The new description to assign to the author. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the description
    /// was successfully updated; otherwise, <see langword="false"/>.</returns>
    Task<bool> UpdateAuthorDescriptionAsync(ulong discordId, string description);

    /// <summary>
    /// Asynchronously updates the description of the specified author.
    /// </summary>
    /// <param name="author">The author whose description is to be updated. Cannot be null.</param>
    /// <param name="description">The new description to assign to the author. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the update was
    /// successful; otherwise, <see langword="false"/>.</returns>
    Task<bool> UpdateAuthorDescriptionAsync(Author author, string description);

    /// <summary>
    /// Asynchronously updates the details of an existing author in the data store.
    /// </summary>
    /// <param name="author">The author entity containing the updated information. The author's identifier must correspond to an existing
    /// author in the data store.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the author was
    /// successfully updated; otherwise, <see langword="false"/>.</returns>
    Task<bool> UpdateAuthorAsync(Author author);
}
