using System.Text.Json;
using Neisvestney.Marketplaces.Shared;

namespace Neisvestney.Marketplaces.Ozon;

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
