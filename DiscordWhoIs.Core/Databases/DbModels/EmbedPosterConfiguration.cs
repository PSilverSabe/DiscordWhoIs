using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Core.Databases.DbModels;

public class EmbedPosterConfiguration
{
    [Key]
    public int Id { get; set; }

    public bool Enabled { get; set; } = false;

    public ulong? ChannelId { get; set; } = null;

    /// <summary>
    /// How long to suppress duplicate embeds for the same link in the same channel, in minutes.
    /// </summary>
    public int DeduplicationWindowMinutes { get; set; } = 10;
}
