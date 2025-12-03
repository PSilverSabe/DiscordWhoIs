namespace DiscordWhoIs.Databases.Serializers
{
    using DiscordWhoIs.Databases.DataModels;
    using System.Text.Json.Serialization;

    // For each type you want to serialize
    [JsonSerializable(typeof(FicInfo))]
    [JsonSerializable(typeof(IEnumerable<FicInfo>))]
    public partial class CacheJsonContext : JsonSerializerContext
    {
    }

}
