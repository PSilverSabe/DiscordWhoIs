namespace DiscordWhoIs.Configuration.Models
{
    public class DiscordConfiguration
    {
        public required string Token { get; set; }

        public ulong? AllowRoleId { get; set; } = null;

        public bool DevMode { get; set; } = false;

        public ulong? DevGuildId { get; set; } = null;
    }
}
