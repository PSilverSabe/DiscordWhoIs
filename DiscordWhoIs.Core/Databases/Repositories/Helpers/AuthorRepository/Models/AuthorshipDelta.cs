using DiscordWhoIs.Core.Databases.DbModels;

namespace DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository.Models;

public sealed record AuthorshipDelta(
    IReadOnlyList<Author> Added,
    IReadOnlyList<Author> Removed)
{
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0;
}
