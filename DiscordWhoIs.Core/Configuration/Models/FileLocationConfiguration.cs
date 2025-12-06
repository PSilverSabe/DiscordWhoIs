namespace DiscordWhoIs.Core.Configuration.Models
{
    public class FileLocationConfiguration
    {
        public required string TargetDirectory { get; set; }

        public required string FileName { get; set; }
    }
}
