using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Core.Configuration.JsonConverters
{
    public sealed class EmbeddedJsonStringConverter<T>
        :JsonConverter<T> where T : class, new ()
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            // Handle string token
            if (reader.TokenType == JsonTokenType.String)
            {
                string? raw = reader.GetString();

                if (string.IsNullOrWhiteSpace(raw))
                    return new T();

                // If the string starts with [ or {, treat it as JSON
                raw = raw.Trim();
                if ((raw.StartsWith("[") && raw.EndsWith("]")) ||
                    (raw.StartsWith("{") && raw.EndsWith("}")))
                {
                    return JsonSerializer.Deserialize<T>(raw, options);
                }

                // Handle List<string> special case
                if (typeof(T) == typeof(List<string>))
                {
                    return new List<string> { raw } as T;
                }

                // Otherwise, treat as raw string cast (for T = string)
                if (typeof(T) == typeof(string))
                {
                    return raw as T;
                }

                // Unknown type, return empty
                return new T();
            }

            // Otherwise, standard JSON token (array/object/etc.)
            return JsonSerializer.Deserialize<T>(ref reader, options);
        }

        public override void Write(
            Utf8JsonWriter writer,
            T value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
