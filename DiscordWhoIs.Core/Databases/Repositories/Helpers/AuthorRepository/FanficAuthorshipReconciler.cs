using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository.Models;

namespace DiscordWhoIs.Core.Databases.Repositories.Helpers.AuthorRepository;

public static class FanficAuthorshipReconciler
{
    public static AuthorshipDelta Reconcile(
        Fanfic existing,
        Fanfic incoming)
    {
        var existingById = existing.Authors
            .ToDictionary(a => a.AuthorId);

        var incomingById = incoming.Authors
            .ToDictionary(a => a.AuthorId);

        var added = new List<Author>();
        var removed = new List<Author>();

        // Add new authors
        foreach (Author author in incomingById.Values)
        {
            if (!existingById.ContainsKey(author.AuthorId))
            {
                existing.Authors.Add(author);
                added.Add(author);
            }
        }

        // Remove stale authors
        for (int i = existing.Authors.Count - 1; i >= 0; i--)
        {
            Author author = existing.Authors.ElementAt(i);
            if (!incomingById.ContainsKey(author.AuthorId))
            {
                existing.Authors.Remove(author);
                removed.Add(author);
            }
        }

        return new AuthorshipDelta(added, removed);
    }
}
