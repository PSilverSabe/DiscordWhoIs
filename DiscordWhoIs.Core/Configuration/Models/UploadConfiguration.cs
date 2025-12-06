namespace DiscordWhoIs.Core.Configuration.Models
{
    public class UploadConfiguration : FileLocationConfiguration
    {
        public bool IncludeExceptionDetails { get; set; } = false;

        public required string ApiKey { get; set; }
    }
}
