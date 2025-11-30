using DiscordWhoIs.Databases.DataModels;

namespace DiscordWhoIs.Models
{
    public class Ao3ResponseStatus
    {
        public IEnumerable<FicInfo> Fics { get; set; } = Array.Empty<FicInfo>();

        public bool IsSuccessful { get; set; }
    }
}
