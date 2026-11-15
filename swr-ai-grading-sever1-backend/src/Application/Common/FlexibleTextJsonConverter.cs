using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Common;

/// <summary>
/// Reads a JSON value that the AI may return either as a string or as a number
/// (for example criterion numbers such as 1, 2, "8.d").
/// </summary>
public sealed class FlexibleTextJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            case JsonTokenType.Null:
                return string.Empty;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var integer)
                    ? integer.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean().ToString();
            default:
                using (var document = JsonDocument.ParseValue(ref reader))
                    return document.RootElement.GetRawText();
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
