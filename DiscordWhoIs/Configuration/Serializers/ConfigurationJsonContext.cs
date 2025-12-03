using DiscordWhoIs.Configuration.Models;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Databases.Serializers
{
    [JsonSerializable(typeof(BotDbContextConfiguration))]
    [JsonSerializable(typeof(DiscordConfiguration))]
    [JsonSerializable(typeof(FandomConfiguration))]
    public partial class ConfigurationJsonContext : JsonSerializerContext { }

}
