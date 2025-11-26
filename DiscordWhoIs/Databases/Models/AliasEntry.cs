using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.Models
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public sealed class AliasEntry
    {
        public AliasEntry() { }

        public AliasEntry(string alias, string real, string? description = null)
        {
            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
            Real = real ?? throw new ArgumentNullException(nameof(real));
            Description = description;
        }

        public string Alias { get; set; } = null!;

        public string Real { get; set; } = null!;

        public string? Description { get; set; }
    }
}
