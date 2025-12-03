using System.ComponentModel.DataAnnotations;

namespace DiscordWhoIs.Configuration.Models
{
    public class BotDbContextConfiguration
    {
        [Required]
        public required string Path { get; set; }
    }
}
