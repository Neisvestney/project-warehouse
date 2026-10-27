using System.Text.Json;
using Neisvestney.Marketplaces.Shared;

namespace Neisvestney.Marketplaces.Wildberries;

/// <summary>
/// Shared setup of every generated client, wired in by <c>Generated/WildberriesClientHooks.g.cs</c>:
/// NSwag gives each client class its own serializer options and partial hooks.
/// </summary>
internal static class WildberriesClientSetup
{
    // NSwag emits no converter for enums inside collections, so an enum array would deserialize as ints
    // and throw. Registering the converter on the options covers every enum at any depth.
    public static void ConfigureSerializer(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new TolerantEnumConverter());
        settings.Converters.Add(new TolerantStringConverter());
        // optional body fields left unset are omitted, not sent as null the spec does not allow
        settings.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    }

    // Streaming (the NSwag default) leaves WildberriesApiException.Response empty exactly when the payload
    // matters; the switch is per client, and the read-only responses are page-sized.
    public const bool ReadResponseAsString = true;
}
