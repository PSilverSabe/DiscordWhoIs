namespace DiscordWhoIs.Controllers.Models
{
    // Small explicit DTO prevents anonymous-type trimming issues when trimming/linking.
    public sealed record PingResponse(string Status);
}