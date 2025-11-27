using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Configuration.Models
{
    public class AliasConfiguration
    {
        [Required]
        public required string Path { get; set; }
    }
}
