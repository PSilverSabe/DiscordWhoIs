using System.Text.Json.Serialization;
using DiscordWhoIs.Core.Configuration.JsonConverters;
using DiscordWhoIs.Core.Databases.DbModels;


namespace DiscordWhoIs.Core.Configuration.Models;

public class FanficJsonImport
{
    public int Id { get; set; }

    public required string Link { get; set; } = null!;

    public required string Title { get; set; } = null!;

    [JsonConverter(typeof(EmbeddedJsonStringConverter<List<string>>))]
    public required List<string> Authors { get; set; } = null!;

    [JsonIgnore]
    private List<Author> MappedAuthors { get; set; } = [];

    public required string Summary { get; set; } = null!;

    public required int WordCount { get; set; }

    public required int HitCount { get; set; }

    public required int CommentsCount { get; set; }

    public required int KudosCount { get; set; }

    public required int BookmarksCount { get; set; }

    public required int ChaptersCount { get; set; }

    public required string Rating { get; set; } = null!;

    public required string Warnings { get; set; } = null!;

    public required string Category { get; set; } = null!;

    [JsonConverter(typeof(Ao3LastUpdateConverter))]
    public required DateTime FicLastUpdated { get; set; }

    public required DateTime DateAdded { get; set; }

    public required DateTime DateUpdated { get; set; }
}
