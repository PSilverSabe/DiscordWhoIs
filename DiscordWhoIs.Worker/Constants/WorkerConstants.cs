namespace DiscordWhoIs.Worker;

/// <summary>
/// Central place for constants used by the Worker Constants.
/// </summary>
public static class WorkerConstants
{
    // Discord embed limits
    public const int EmbedFieldMaxLength = 1024;
    public const int EmbedMaxFields = 25;

    // Discord message limits
    public const int MessageMaxLength = 2000;

    // Discord get messages limit for purging
    public const int DiscordGetMessagesLimit = 100;

    // How many recent works to include by default
    public const int RecentWorksDefaultLimit = 10;

    // Truncation lengths used in the UI
    public const int TitleMaxLength = 256;
    public const int TitleTruncateReserve = 3; // for "..."

    // AO3 / presentation constants
    public const string Ao3ProfileUrlFormat = "https://archiveofourown.org/users/{0}";
    public const string Ao3FooterText = "Source: Archive of Our Own";
    public const string Ao3FooterIcon = "https://archiveofourown.org/images/ao3_logos/logo_42.png";
}
