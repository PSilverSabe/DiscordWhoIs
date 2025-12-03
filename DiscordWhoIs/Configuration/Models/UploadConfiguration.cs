namespace DiscordWhoIs.Configuration.Models
{
    public class UploadConfiguration
    {
        public required string UploadDirectory { get; set; }

        public required string FileName { get; set; }

        public bool IncludeExceptionDetails { get; set; } = false;

        public required string ApiKey { get; set; }
    }
}
