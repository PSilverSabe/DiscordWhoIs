namespace DiscordWhoIs.Core.Databases.DbModels
{
    using CsvHelper.Configuration.Attributes;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Fanfic
    {
        [Key]
        [Ignore]
        public int Id { get; set; }

        [Name("link")]
        public required string Link { get; set; } = null!;

        [Name("title")]
        public required string Title { get; set; } = null!;

        [Name("author")]
        public required string Author { get; set; } = null!;

        [Name("summary")]
        public required string Summary { get; set; } = null!;

        [Name("words")]
        public required int WordCount { get; set; }

        [Name("hits")]
        public required int HitCount { get; set; }

        [Name("comments")]
        public required int CommentCount { get; set; }

        [Name("kudos")]
        public required int KudosCount { get; set; }

        [Name("bookmarks")]
        public required int BookmarksCount { get; set; }

        [Name("chapters")]
        public required int ChapterCount { get; set; }

        [Name("rating")]
        public required string Rating { get; set; } = null!;

        [Name("warnings")]
        public required string Warnings { get; set; } = null!;

        [Name("category")]
        public required string Category { get; set; } = null!;

        [Name("fic_last_updated")]
        public required DateTime FicLastUpdated { get; set; }

        [Name("date_added")]
        public required DateTime DateAdded { get; set; }

        [Name("date_updated")]
        public required DateTime DateUpdated { get; set; }
    }
}
