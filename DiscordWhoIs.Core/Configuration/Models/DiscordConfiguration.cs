namespace DiscordWhoIs.Core.Configuration.Models;

public class DiscordConfiguration
{
    public string Token { get; set; } = string.Empty;

    public ulong? AllowRoleId { get; set; } = null;

    public bool DevMode { get; set; } = false;

    public ulong? DevGuildId { get; set; } = null;
}
