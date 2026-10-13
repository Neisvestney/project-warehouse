using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Neisvestney.Marketplaces.Generator;

public static class SpecLoader
{
    public static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static JsonObject Load(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".yaml" or ".yml" ? LoadYaml(path) : LoadJson(path);

    public static JsonObject LoadJson(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    public static JsonObject LoadYaml(string path)
    {
        var stream = new YamlStream();
        using (var reader = new StreamReader(path))
            stream.Load(reader);
        return ToJson(stream.Documents[0].RootNode)!.AsObject();
    }

    public static void Save(JsonNode document, string path) =>
        File.WriteAllText(path, document.ToJsonString(WriteOptions));

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode mapping => new JsonObject(mapping.Children.Select(c =>
            KeyValuePair.Create(((YamlScalarNode)c.Key).Value!, ToJson(c.Value)))),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ToJson).ToArray()),
        YamlScalarNode scalar => ToJson(scalar),
        _ => throw new InvalidOperationException($"Unsupported YAML node {node.NodeType} at {node.Start}"),
    };

    // YAML core schema: only plain scalars are typed, a quoted "1" stays a string
    private static JsonNode? ToJson(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style is not ScalarStyle.Plain)
            return JsonValue.Create(value);

        return value switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            _ when long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l) => JsonValue.Create(l),
            _ when IsYamlFloat(value) => JsonValue.Create(double.Parse(value, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value),
        };
    }

    private static bool IsYamlFloat(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$");
}
