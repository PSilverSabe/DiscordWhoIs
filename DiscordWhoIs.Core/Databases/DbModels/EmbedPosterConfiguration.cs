using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordWhoIs.Core.Databases.DbModels;

public class EmbedPosterConfiguration
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(ServerId))]
    public int ServerId { get; set; }

    public required ulong ChannelId { get; set; }

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// How long to suppress duplicate embeds for the same link in this channel, in minutes.
    /// </summary>
    public int DeduplicationWindowMinutes { get; set; } = 10;

    public required DateTime CreatedDate { get; set; }

    public required DateTime UpdatedDate { get; set; }

    public virtual Server Server { get; set; } = null!;
}
