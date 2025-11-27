namespace DiscordWhoIs.Databases.DbModels
{
    public sealed class AliasEntry
    {
        public AliasEntry() { }

        public AliasEntry(string alias, string real)
        {
            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
            Real = real ?? throw new ArgumentNullException(nameof(real));
        }

        public string Alias { get; set; } = null!;

        public string Real { get; set; } = null!;
    }
}
