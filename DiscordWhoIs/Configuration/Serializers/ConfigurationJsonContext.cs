using DiscordWhoIs.Configuration.Models;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Databases.Serializers
{
    [JsonSerializable(typeof(AliasConfiguration))]
    [JsonSerializable(typeof(Ao3Configuration))]
    [JsonSerializable(typeof(CacheConfiguration))]
    [JsonSerializable(typeof(DiscordConfiguration))]
    [JsonSerializable(typeof(FandomConfiguration))]
    [JsonSerializable(typeof(ProxyConfiguration))]
    public partial class ConfigurationJsonContext : JsonSerializerContext { }

}
