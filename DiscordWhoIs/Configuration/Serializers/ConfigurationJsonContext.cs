using DiscordWhoIs.Configuration.Models;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Databases.Serializers
{
    [JsonSerializable(typeof(BotDbContextConfiguration))]
    [JsonSerializable(typeof(DiscordConfiguration))]
    [JsonSerializable(typeof(FandomConfiguration))]
    [JsonSerializable(typeof(UploadConfiguration))]
    public partial class ConfigurationJsonContext : JsonSerializerContext { }

}
