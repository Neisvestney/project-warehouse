using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neisvestney.Marketplaces.Shared;

/// <summary>Reads a JSON number (or boolean) into a <see cref="string"/> property instead of throwing.</summary>
/// <remarks>
/// Marketplaces do not keep the string/number boundary their own specs declare (Ozon sends <c>sku</c> as a
/// number and int64 SKUs inside a string array). The numeric text is taken verbatim, preserving int64 values
/// that would lose precision through a double.
/// </remarks>
internal class TolerantStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(
                reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot read a string from a {reader.TokenType} token."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
