using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Core.Databases.DbModels;

public class Fanfic
{
    [Key]
    public int FanficId { get; set; }

    public required string Link { get; set; } = null!;

    public required string Title { get; set; } = null!;

    public required string Summary { get; set; } = null!;

    public required int WordCount { get; set; }

    public required int HitCount { get; set; }

    public required int CommentCount { get; set; }

    public required int KudosCount { get; set; }

    public required int BookmarksCount { get; set; }

    public required int ChapterCount { get; set; }

    public required string Rating { get; set; } = null!;

    public required string Warnings { get; set; } = null!;

    public required string Category { get; set; } = null!;

    public required DateTime FicLastUpdated { get; set; }

    public required DateTime DateAdded { get; set; }

    public required DateTime DateUpdated { get; set; }

    public virtual ICollection<Author> Authors { get; set; } = [];
}
