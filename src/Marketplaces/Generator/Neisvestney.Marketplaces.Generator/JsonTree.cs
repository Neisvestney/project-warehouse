using System.Text.Json.Nodes;

namespace Neisvestney.Marketplaces.Generator;

public static class JsonTree
{
    public static IEnumerable<string> FindReferences(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o)
                {
                    if (key == "$ref" && value is JsonValue v)
                        yield return v.GetValue<string>();
                    else if (value is not null)
                        foreach (var nested in FindReferences(value))
                            yield return nested;
                }
                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                    foreach (var nested in FindReferences(item))
                        yield return nested;
                break;
        }
    }

    /// <summary>Rewrites every <c>$ref</c> whose full value is a key of <paramref name="renames"/>.</summary>
    public static void RewriteReferences(JsonNode node, IReadOnlyDictionary<string, string> renames)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o.ToArray())
                {
                    if (key == "$ref" && value is JsonValue v && renames.TryGetValue(v.GetValue<string>(), out var target))
                        o[key] = target;
                    else if (value is not null)
                        RewriteReferences(value, renames);
                }
                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                    RewriteReferences(item, renames);
                break;
        }
    }
}
