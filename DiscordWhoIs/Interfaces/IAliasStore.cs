namespace DiscordWhoIs.Interfaces
{
    using System.Collections.Generic;

    // Represents a stored alias mapping with optional description.
    public sealed record AliasEntry(string Alias, string Real, string? Description);

    public interface IAliasStore
    {
        IReadOnlyList<AliasEntry> GetAllAliases();
        bool TryResolve(string alias, out string real);
        bool TryGet(string alias, out AliasEntry? entry);
        void AddOrUpdate(string alias, string real, string? description = null);
        bool Remove(string alias);
    }
}