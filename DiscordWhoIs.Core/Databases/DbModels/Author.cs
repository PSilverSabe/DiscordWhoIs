using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace DiscordWhoIs.Core.Databases.DbModels;

[Index(nameof(Ao3ProfileName), IsUnique = true)]
[Index(nameof(FanficNetId), IsUnique = true)]
[Index(nameof(FanficNetProfileName), IsUnique = true)]
[Index(nameof(DiscordId), IsUnique = true)]
[Index(nameof(DiscordUserName), IsUnique = true)]
public class Author
{
    public Author() { }

    public Author(string ao3ProfileName, int? fanficNetId, string? fanFicNetProfileName, ulong? discordId, string? discordUsername)
    {
        Ao3ProfileName = ao3ProfileName ?? throw new ArgumentNullException(nameof(ao3ProfileName));
        FanficNetId = fanficNetId;
        FanficNetProfileName = fanFicNetProfileName;
        DiscordId = discordId;
        DiscordUserName = discordUsername;
    }

    [Key]
    public int AuthorId { get; set; }

    public required string Ao3ProfileName { get; set; }

    public int? FanficNetId { get; set; }

    public string? FanficNetProfileName { get; set; }

    public ulong? DiscordId { get; set; }

    public string? DiscordUserName { get; set; }

    public string? Description { get; set; }

    public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public required DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public required DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Fanfic> Fanfics { get; set; } = [];

    public virtual ICollection<Alias> Aliases { get; set; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Author Id: {AuthorId}");
        sb.AppendLine($"AO3 Profile Name: {Ao3ProfileName}");
        sb.AppendLine($"FanFic.Net Id: {FanficNetId}");
        sb.AppendLine($"FanFic.Net Profile Name: {FanficNetProfileName}");
        sb.AppendLine($"Discord Id: {DiscordId}");
        sb.AppendLine($"Discord Username: {DiscordUserName}");
        sb.AppendLine($"Created At: {CreatedAt}");
        sb.AppendLine($"Last Updated At: {LastUpdatedAt}");
        sb.AppendLine($"Last Active At: {LastActiveAt}");
        sb.AppendLine($"Number of Fanfics: {Fanfics.Count}");
        return sb.ToString();
    }
}
