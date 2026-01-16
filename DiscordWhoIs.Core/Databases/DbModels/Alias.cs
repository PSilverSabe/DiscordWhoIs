using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordWhoIs.Core.Databases.DbModels;

[Table("Alias")]
public class Alias
{
    public Alias() { }

    public Alias(string alias, int authorId)
    {
        AliasUserName = alias ?? throw new ArgumentNullException(nameof(alias));
        AuthorId = authorId;
    }

    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("AliasName")]
    public string AliasUserName { get; set; } = null!;

    [Column("AuthorId")]
    public int AuthorId { get; set; }

    public virtual Author Author { get; set; } = null!;
}
