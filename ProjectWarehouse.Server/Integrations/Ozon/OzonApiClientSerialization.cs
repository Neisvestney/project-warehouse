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
    }

    // Streaming (the NSwag default) leaves OzonApiException.Response empty exactly when the payload matters —
    // a rejection then reaches MarketplaceSyncRun as a bare status code with nothing to debug. Buffering only
    // failures would need an override, but ReadObjectResponseAsync is declared in the generated half of this
    // class, so the switch is global; pages are capped at a few hundred cards, so the extra string is cheap.
    partial void Initialize() => ReadResponseAsString = true;
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

internal class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => Enum.TryParse<T>(reader.GetString(), true, out var parsed) ? parsed : default,
            JsonTokenType.Number => reader.TryGetInt64(out var number) ? (T)Enum.ToObject(typeof(T), number) : default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot read {typeof(T).Name} from a {reader.TokenType} token."),
        };

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
