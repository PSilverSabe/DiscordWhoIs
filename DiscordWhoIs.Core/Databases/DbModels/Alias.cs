namespace DiscordWhoIs.Core.Databases.DbModels
{
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Alias")]
    public class Alias
    {
        public Alias() { }

        public Alias(string alias, string real)
        {
            AliasUserName = alias ?? throw new ArgumentNullException(nameof(alias));
            RealUserName = real ?? throw new ArgumentNullException(nameof(real));
        }

        [Column("Alias")]
        public string AliasUserName { get; set; } = null!;

        [Column("Real")]
        public string RealUserName { get; set; } = null!;
    }
}
