using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Neisvestney.Marketplaces.Shared;

namespace Neisvestney.Marketplaces.YandexMarket;

public partial class YandexMarketApiClient
{
    // NSwag emits no converter for enums inside collections, so an enum array would deserialize as ints
    // and throw. Registering the converter on the options covers every enum at any depth.
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new TolerantEnumConverter());
        settings.Converters.Add(new TolerantStringConverter());
        settings.Converters.Add(new TimeOnlyConverter());
        settings.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ProtectRoutingEnums },
        };
    }

    private static void ProtectRoutingEnums(JsonTypeInfo type)
    {
        foreach (var property in type.Properties)
        {
            if (property.PropertyType == typeof(SellingProgramType?))
                property.CustomConverter = new RoutingEnumConverter<SellingProgramType>();
            else if (property.PropertyType == typeof(PlacementType?))
                property.CustomConverter = new RoutingEnumConverter<PlacementType>();
            else if (property.PropertyType == typeof(ApiAvailabilityStatusType?))
                property.CustomConverter = new RoutingEnumConverter<ApiAvailabilityStatusType>();
        }
    }

    // Unknown routing values must fail the read, never become the first enum member (FBS or FBY).
    private sealed class RoutingEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
    {
        public override T? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.String && Enum.TryParse<T>(reader.GetString(), true, out var value)
                && Enum.IsDefined(value)) return value;
            throw new JsonException($"Unknown Yandex Market routing value for {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value.ToString());
        }
    }

    // Streaming (the NSwag default) leaves YandexMarketApiException.Response empty exactly when the payload
    // matters; the switch is global because ReadObjectResponseAsync lives in the generated half of this class.
    partial void Initialize() => ReadResponseAsString = true;
}
