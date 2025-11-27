namespace DiscordWhoIs.Databases.Serializers
{
    using DiscordWhoIs.Databases.DataModels;
    using System.Text.Json.Serialization;

    // For each type you want to serialize
    [JsonSerializable(typeof(FicInfo), TypeInfoPropertyName = "FicInfo")]
    [JsonSerializable(typeof(IEnumerable<FicInfo>), TypeInfoPropertyName = "IEnumerableFicInfo")]
    public partial class CacheJsonContext : JsonSerializerContext
    {
    }

}
