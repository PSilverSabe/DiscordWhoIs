namespace DiscordWhoIs.Core.Databases.DbModels
{
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Alias")]
    public class Alias
    {
        public Alias() { }

        public Alias(string alias, int authorId)
        {
            AliasUserName = alias ?? throw new ArgumentNullException(nameof(alias));
            AuthorId = authorId;
        }

        [Column("AliasName")]
        public string AliasUserName { get; set; } = null!;

        [Column("AuthorId")]
        public int AuthorId { get; set; }

        public virtual Author Author { get; set; } = null!;
    }
}
