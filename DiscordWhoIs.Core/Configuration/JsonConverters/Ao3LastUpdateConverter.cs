using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordWhoIs.Core.Configuration.JsonConverters
{
    public sealed class Ao3LastUpdateConverter
         : JsonConverter<DateTime>
    {
        private static readonly string[] Formats =
        {
            "dd MMM yyyy"
        };

        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected date string.");

            string value = reader.GetString()!;

            if (DateTime.TryParseExact(
                value,
                Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime result))
            {
                return result;
            }

            throw new JsonException($"Invalid date format: {value}");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime());
        }
    }
}
