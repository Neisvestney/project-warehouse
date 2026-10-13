using System.Text.Json;
using Neisvestney.Marketplaces.Shared;

namespace Neisvestney.Marketplaces.Avito;

public partial class AvitoApiClient
{
    // The generator rewrites Avito enums to plain strings; the converters stay as the net every marketplace
    // client shares, so an enum that slips through later never fails a whole page.
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new TolerantEnumConverter());
        settings.Converters.Add(new TolerantStringConverter());
    }

    // Streaming (the NSwag default) leaves AvitoApiException.Response empty exactly when the payload matters;
    // the switch is global, and the JSON responses are page-sized. Label PDFs still stream as FileResponse.
    partial void Initialize() => ReadResponseAsString = true;
}
