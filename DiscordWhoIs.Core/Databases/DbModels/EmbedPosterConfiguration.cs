using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordWhoIs.Core.Databases.DbModels;

public class EmbedPosterConfiguration
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(ServerId))]
    public ulong? ServerId { get; set; } = 0;

    public bool Enabled { get; set; } = false;

    public ulong? ChannelId { get; set; } = null;

    /// <summary>
    /// How long to suppress duplicate embeds for the same link in the same channel, in minutes.
    /// </summary>
    public int DeduplicationWindowMinutes { get; set; } = 10;

    public virtual Server? Server { get; set; } = null;
}
