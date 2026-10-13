using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neisvestney.Marketplaces.YandexMarket;

/// <summary>
/// Reads <c>format: time</c> values such as delivery intervals (<c>"09:00"</c>), which the built-in
/// <see cref="TimeOnly"/> converter rejects for lacking seconds. Anything else fails the response.
/// </summary>
internal class TimeOnlyConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
        && TimeOnly.TryParseExact(reader.GetString(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new JsonException($"Cannot read a time of day from {reader.TokenType} token.");

    // the API documents HH:MM; seconds are written only when there are any
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(value is { Second: 0, Millisecond: 0, Microsecond: 0 } ? "HH:mm" : "HH:mm:ss.FFFFFFF",
            CultureInfo.InvariantCulture));
}
