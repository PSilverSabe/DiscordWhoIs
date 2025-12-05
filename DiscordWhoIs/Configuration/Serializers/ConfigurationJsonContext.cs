using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Controllers.Models;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Databases.Serializers
{
    [JsonSerializable(typeof(FileLocationConfiguration))]
    [JsonSerializable(typeof(DiscordConfiguration))]
    [JsonSerializable(typeof(FandomConfiguration))]
    [JsonSerializable(typeof(UploadConfiguration))]
    [JsonSerializable(typeof(PingResponse))] // preserve source-gen metadata for PingResponse
    [JsonSerializable(typeof(OperationResult))] // preserve source-gen metadata for OperationResult
    public partial class ConfigurationJsonContext : JsonSerializerContext { }

}
