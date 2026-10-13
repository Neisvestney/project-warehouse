using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neisvestney.Marketplaces.Shared;

/// <summary>
/// Enums that degrade to the default member instead of throwing: marketplaces extend their enums without
/// warning, and the committed spec is a snapshot. Registered on the options for enums inside collections,
/// and put on enum properties by the generator in place of NSwag's <c>JsonStringEnumConverter&lt;T&gt;</c>.
/// </summary>
internal class TolerantEnumConverter : JsonConverterFactory
{
    // Nullable enums resolve through the underlying type, so matching on IsEnum alone covers both
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert))!;
}

/// <remarks>
/// The wire value is the member's <c>[EnumMember]</c> value (<c>"Склад WB"</c> → <c>Склад_WB</c>), falling back to
/// the member name. Integer enums (cargoType: 1, 2, 3) come out of NJsonSchema as members named _1, _2 … and
/// travel as numbers.
/// </remarks>
internal class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly bool IsNumeric = Enum.GetNames<T>() is { Length: > 0 } names
        && names.All(n => n.Length > 1 && n[0] == '_' && n[1..].All(char.IsAsciiDigit));

    private static readonly Dictionary<T, string> ToWire = Enum.GetValues<T>().Distinct().ToDictionary(
        v => v,
        v => typeof(T).GetField(v.ToString())!.GetCustomAttributes(typeof(EnumMemberAttribute), false)
            .OfType<EnumMemberAttribute>().FirstOrDefault()?.Value ?? v.ToString());

    private static readonly Dictionary<string, T> FromWire = ToWire
        .GroupBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            // Enum.TryParse and Enum.ToObject accept any number, so undefined values are filtered out
            JsonTokenType.String => reader.GetString() is { } s
                && (FromWire.TryGetValue(s, out var mapped) || Enum.TryParse(s, true, out mapped) && Enum.IsDefined(mapped))
                ? mapped
                : default,
            JsonTokenType.Number => reader.TryGetInt64(out var number) && Enum.ToObject(typeof(T), number) is T value && Enum.IsDefined(value)
                ? value
                : default,
            JsonTokenType.Null => default,
            _ => throw new JsonException($"Cannot read {typeof(T).Name} from a {reader.TokenType} token."),
        };

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (IsNumeric)
            writer.WriteNumberValue(Convert.ToInt64(value));
        else
            writer.WriteStringValue(ToWire.GetValueOrDefault(value) ?? value.ToString());
    }
}
