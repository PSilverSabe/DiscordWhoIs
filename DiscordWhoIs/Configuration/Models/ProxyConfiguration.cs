namespace DiscordWhoIs.Configuration.Models
{
    public class ProxyConfiguration
    {
        public bool IsEnabled { get; set; }
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
