using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Core.Databases.DbModels;

public class Server
{
    [Key]
    public int ServerId { get; set; }

    public required string ServerName { get; set; } = null!;

    public required DateTime CreatedDate { get; set; }

    public required DateTime UpdatedDate { get; set; }

    public virtual ICollection<EmbedPosterConfiguration> EmbedPosterConfigurations { get; set; } = [];
}
