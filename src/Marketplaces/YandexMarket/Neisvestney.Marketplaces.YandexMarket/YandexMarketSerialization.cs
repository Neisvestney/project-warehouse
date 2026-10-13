using System.Text.Json;
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
    }

    // Streaming (the NSwag default) leaves YandexMarketApiException.Response empty exactly when the payload
    // matters; the switch is global because ReadObjectResponseAsync lives in the generated half of this class.
    partial void Initialize() => ReadResponseAsString = true;
}
