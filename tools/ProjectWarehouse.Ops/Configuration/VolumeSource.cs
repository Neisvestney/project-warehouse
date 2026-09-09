using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Ops.Configuration;

/// Where a logical volume lives on the target. Written either as a bare compose volume name or as
/// `{ "path": "..." }` for a host directory the compose file binds in — a stack that keeps its data
/// in bind mounts has no named volume to resolve.
[JsonConverter(typeof(VolumeSourceConverter))]
public sealed class VolumeSource
{
    public string? Volume { get; set; }

    public string? Path { get; set; }

    /// What goes on the left of a `docker run -v` mount. A bind path has to be absolute, or docker
    /// reads it as a volume name instead.
    public string MountSource => Path ?? Volume ?? string.Empty;

    public override string ToString() => MountSource;
}

public sealed class VolumeSourceConverter : JsonConverter<VolumeSource>
{
    public override VolumeSource Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new VolumeSource { Volume = reader.GetString() };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A volume is either a compose volume name or { \"path\": \"...\" }.");

        var source = new VolumeSource();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var field = reader.GetString();
            reader.Read();

            switch (field?.ToLowerInvariant())
            {
                case "volume":
                    source.Volume = reader.GetString();
                    break;
                case "path":
                    source.Path = reader.GetString();
                    break;
                default:
                    throw new JsonException(
                        $"Unknown volume field '{field}'. Known fields: volume, path.");
            }
        }

        return source;
    }

    public override void Write(Utf8JsonWriter writer, VolumeSource value, JsonSerializerOptions options)
    {
        if (value.Path is { } path)
        {
            writer.WriteStartObject();
            writer.WriteString("path", path);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStringValue(value.Volume);
        }
    }
}
