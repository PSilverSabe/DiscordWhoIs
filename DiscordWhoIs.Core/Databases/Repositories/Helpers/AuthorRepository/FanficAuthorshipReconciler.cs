using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository.Models;

namespace DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository;

public static class FanficAuthorshipReconciler
{
    public static AuthorshipDelta Reconcile(
        Fanfic existing,
        Fanfic incoming)
    {
        var existingAuthorsById = existing.Authors
            .GroupBy(a => a.AuthorId)
            .Select(g => g.First())
            .ToDictionary(a => a.AuthorId);

        var incomingAuthorsById = incoming.Authors
            .GroupBy(a => a.AuthorId)
            .Select(g => g.First())
            .ToDictionary(a => a.AuthorId);

        var added = incomingAuthorsById.Values
            .Where(a => !existingAuthorsById.ContainsKey(a.AuthorId))
            .ToList();

        var removed = existingAuthorsById.Values
            .Where(a => !incomingAuthorsById.ContainsKey(a.AuthorId))
            .ToList();

        foreach (Author? author in added)
        {
            existing.Authors.Add(author);
        }

        foreach (Author? author in removed)
        {
            existing.Authors.Remove(author);
        }

        return new AuthorshipDelta(added, removed);
    }
}
