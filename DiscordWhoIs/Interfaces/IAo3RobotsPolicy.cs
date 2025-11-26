namespace DiscordWhoIs.Interfaces
{
    public interface IAo3RobotsPolicy
    {
        bool IsPathAllowed(string url);
        Task EnforceRateLimitAsync();
    }
}
