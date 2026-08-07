using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Integrations.Ozon.Generated;

public partial class OzonApiClient
{
    // NSwag emits no converter for enums inside collections ("TODO: Add ItemConverterType ..."), so
    // working_days: ["MONDAY"] would deserialize as an int and throw. Registering the converter on the
    // options covers every enum at any depth.
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new TolerantEnumConverter());
        settings.Converters.Add(new TolerantStringConverter());
    }

    // Streaming (the NSwag default) leaves OzonApiException.Response empty exactly when the payload matters —
    // a rejection then reaches MarketplaceSyncRun as a bare status code with nothing to debug. Buffering only
    // failures would need an override, but ReadObjectResponseAsync is declared in the generated half of this
    // class, so the switch is global; pages are capped at a few hundred cards, so the extra string is cheap.
    partial void Initialize() => ReadResponseAsString = true;
}

/// <summary>
/// Reads a JSON number (or boolean) into a <see cref="string"/> property instead of throwing.
/// </summary>
/// <remarks>
/// Ozon does not keep the string/number boundary its own spec declares: <c>price</c> arrives as a string,
/// <c>sku</c> as a number, and <c>requirements.products_requiring_gtd</c> is typed as an array of strings
/// yet carries int64 SKUs — which failed one whole page of postings over a single field. Patching each
/// offender in the trimmed spec only holds until the next one, so the numeric text is taken verbatim,
/// preserving int64 values that would lose precision through a double.
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

/// <summary>
/// String enums that degrade to the default member instead of throwing. Ozon extends its enums without
/// warning, and the committed spec is a snapshot — an unknown value must not kill a whole sync page.
/// </summary>
internal class TolerantEnumConverter : JsonConverterFactory
{
    // Nullable enums resolve through the underlying type, so matching on IsEnum alone covers both.
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert))!;
}

// System.Enum is spelled out: NSwag names inline anonymous enums Enum, Enum2, Enum3 …, and the
// generated `enum Enum` sits in this very namespace, shadowing the framework type.
internal class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, System.Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => System.Enum.TryParse<T>(reader.GetString(), true, out var parsed) ? parsed : default,
            JsonTokenType.Number => reader.TryGetInt64(out var number) ? (T)System.Enum.ToObject(typeof(T), number) : default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot read {typeof(T).Name} from a {reader.TokenType} token."),
        };

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
