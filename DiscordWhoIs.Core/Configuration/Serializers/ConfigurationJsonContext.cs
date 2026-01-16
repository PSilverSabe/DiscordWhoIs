using System.Text.Json.Serialization;
using DiscordWhoIs.Core.Configuration.Models;

namespace DiscordWhoIs.Databases.Serializers;

[JsonSerializable(typeof(FileLocationConfiguration))]
[JsonSerializable(typeof(DiscordConfiguration))]
[JsonSerializable(typeof(FandomConfiguration))]
[JsonSerializable(typeof(UploadConfiguration))]
public partial class ConfigurationJsonContext : JsonSerializerContext { }
