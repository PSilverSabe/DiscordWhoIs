using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DiscordWhoIs.Core.Databases.DbModels
{
    [Index(nameof(Ao3ProfileName), IsUnique = true)]
    [Index(nameof(FanficNetId), IsUnique = true)]
    [Index(nameof(FanficNetProfileName), IsUnique = true)]
    [Index(nameof(DiscordId), IsUnique = true)]
    [Index(nameof(DiscordUsername), IsUnique = true)]
    public class Author
    {
        public Author() { }

        public Author(string ao3ProfileName, int? fanficNetId, string? fanFicNetProfileName, int? discordId, string? discordUsername)
        {
            Ao3ProfileName = ao3ProfileName ?? throw new ArgumentNullException(nameof(ao3ProfileName));
            FanficNetId = fanficNetId;
            FanficNetProfileName = fanFicNetProfileName;
            DiscordId = discordId;
            DiscordUsername = discordUsername;
        }

        [Key]
        public int AuthorId { get; set; }

        public required string Ao3ProfileName { get; set; }

        public int? FanficNetId { get; set; }

        public string? FanficNetProfileName { get; set; }

        public int? DiscordId { get; set; }

        public string? DiscordUsername { get; set; }

        public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public required DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        public required DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Fanfic> Fanfics { get; set; } = [];

        public virtual ICollection<Alias> Aliases { get; set; } = [];

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Author Id: {Ao3ProfileName}");
            sb.AppendLine($"AO3 Profile Name: {Ao3ProfileName}");
            sb.AppendLine($"FanFic.Net Id: {FanficNetId}");
            sb.AppendLine($"FanFic.Net Profile Name: {FanficNetProfileName}");
            sb.AppendLine($"Discord Id: {DiscordId}");
            sb.AppendLine($"Discord Username: {DiscordUsername}");
            sb.AppendLine($"Created At: {CreatedAt}");
            sb.AppendLine($"Last Updated At: {LastUpdatedAt}");
            sb.AppendLine($"Last Active At: {LastActiveAt}");
            sb.AppendLine($"Number of Fanfics: {Fanfics.Count}");
            return sb.ToString();
        }
    }
}
