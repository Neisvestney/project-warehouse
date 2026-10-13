using System.Text.Json.Nodes;

namespace Neisvestney.Marketplaces.Generator;

public static class SpecSanitizer
{
    private static readonly HashSet<string> SchemaTypes = ["object", "array", "string", "integer", "number", "boolean", "null"];

    /// <summary>
    /// Fixes the Swagger 2.0 leftovers NJsonSchema cannot read in specs that declare OpenAPI 3: a boolean
    /// `required` on a property schema (dropped: 3.0 only knows a names array on the owner), array schemas
    /// that carry `items` without `"type": "array"` (silently generated as `object`), and prose in `type`.
    /// </summary>
    public static void Sanitize(JsonNode node)
    {
        // Walks the document, not the schemas: descends until it reaches a position that holds a schema.
        // A blind recursion would mistake a `properties` map for a schema whenever a property is called
        // "items" or "type" — the Ozon spec has both.
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o.ToArray())
                {
                    if (value is null)
                        continue;
                    if (key == "schema")
                        SanitizeSchema(value);
                    else if (key == "schemas")
                        foreach (var schema in value.AsObject().Select(p => p.Value).OfType<JsonNode>())
                            SanitizeSchema(schema);
                    else
                        Sanitize(value);
                }
                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                    Sanitize(item);
                break;
        }
    }

    private static void SanitizeSchema(JsonNode node)
    {
        if (node is not JsonObject schema)
            return;

        if (schema["required"] is JsonValue required && required.TryGetValue<bool>(out _))
            schema.Remove("required");

        // Ozon writes prose into `type` in places (v3PostingProductDetail.jw_uin: "array of strings").
        // Printing every rewrite is deliberate: a silent fix is how the next such landmine gets missed.
        if (schema["type"] is JsonValue type && type.TryGetValue<string>(out var typeName)
            && !SchemaTypes.Contains(typeName))
        {
            Console.WriteLine($"  type: \"{typeName}\" is not a JSON Schema type -> rewritten");
            schema.Remove("type");
            if (typeName.Contains("array", StringComparison.OrdinalIgnoreCase) && schema["items"] is null)
                schema["items"] = new JsonObject { ["type"] = "string" };
        }

        if (schema["items"] is JsonObject && !schema.ContainsKey("type"))
            schema["type"] = "array";

        if (schema["properties"] is JsonObject properties)
            foreach (var property in properties.Select(p => p.Value).OfType<JsonNode>())
                SanitizeSchema(property);

        foreach (var key in (string[])["items", "additionalProperties", "not"])
            if (schema[key] is JsonObject nested)
                SanitizeSchema(nested);

        foreach (var key in (string[])["allOf", "anyOf", "oneOf"])
            if (schema[key] is JsonArray branches)
                foreach (var branch in branches.OfType<JsonNode>())
                    SanitizeSchema(branch);
    }

    /// <summary>
    /// Drops every <c>description</c> and <c>summary</c> so the generated client carries no prose from a spec
    /// whose terms forbid copying it: NSwag turns them into XML docs and exception messages.
    /// </summary>
    public static void StripDocumentation(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o.ToArray())
                {
                    // a string value tells the keyword apart from a property named "description", whose value is a schema
                    if (key is "description" or "summary" && value is JsonValue v && v.TryGetValue<string>(out _))
                        o.Remove(key);
                    else if (value is not null)
                        StripDocumentation(value);
                }
                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                    StripDocumentation(item);
                break;
        }
    }

    /// <summary>
    /// Renames dotted component schemas (`posting.v4.…SortDir.Enum`) to a flat PascalCase name.
    /// NJsonSchema types them by the last dot-segment, so every `….Enum` becomes `Enum` and NSwag pulls
    /// them apart with positional suffixes — `Enum2`, `Products2` — that shift whenever the spec changes.
    /// A generated type called `Enum` also shadows `System.Enum` inside the generated namespace.
    /// </summary>
    public static void FlattenDottedSchemaNames(JsonObject document)
    {
        var schemas = document["components"]?["schemas"]?.AsObject();
        if (schemas is null)
            return;

        var taken = schemas.Select(s => s.Key).Where(k => !k.Contains('.')).ToHashSet(StringComparer.Ordinal);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in schemas.Select(s => s.Key).Where(k => k.Contains('.')).OrderBy(k => k, StringComparer.Ordinal))
        {
            var segments = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var full = string.Concat(segments.Select(Pascal));
            // Leading "posting" / "v4" segments are namespace noise; keep them only to break a tie
            var candidate = string.Concat(segments.SkipWhile(IsNoise).Select(Pascal));

            if (candidate.Length == 0 || !taken.Add(candidate))
            {
                candidate = full;
                for (var i = 2; !taken.Add(candidate); i++)
                    candidate = full + i;
            }
            renames[name] = candidate;
        }

        if (renames.Count == 0)
            return;

        var renamed = new JsonObject();
        foreach (var (key, value) in schemas.ToArray())
        {
            schemas.Remove(key);
            renamed[renames.GetValueOrDefault(key, key)] = value;
        }
        document["components"]!.AsObject()["schemas"] = renamed;

        JsonTree.RewriteReferences(document, renames.ToDictionary(r => "#/components/schemas/" + r.Key, r => "#/components/schemas/" + r.Value));
        Console.WriteLine($"  flattened {renames.Count} dotted schema name(s)");

        static bool IsNoise(string segment) =>
            segment.All(char.IsLower) || (segment.Length > 1 && segment[0] is 'v' && segment[1..].All(char.IsAsciiDigit));

        static string Pascal(string segment) =>
            segment.Length == 0 ? segment : char.ToUpperInvariant(segment[0]) + segment[1..];
    }

    /// <summary>
    /// Moves inline request, response and enum-parameter schemas into components named after the operation
    /// (<c>GetV3OrdersResponse</c>, <c>PostV3OrdersStickersType</c>). Left inline, NSwag names them
    /// <c>Response2…Response23</c> by position, and every whitelist edit renumbers them.
    /// Schemas with identical bodies share one component.
    /// </summary>
    public static void NameInlineSchemas(JsonObject document)
    {
        var components = document["components"] as JsonObject ?? (JsonObject)(document["components"] = new JsonObject());
        var schemas = components["schemas"] as JsonObject ?? (JsonObject)(components["schemas"] = new JsonObject());
        var hoisted = 0;

        // array components with an inline item object: NJsonSchema names the item Anonymous
        foreach (var (name, schema) in schemas.ToArray().OrderBy(s => s.Key, StringComparer.Ordinal))
            if (schema is JsonObject s && s["items"] is JsonObject items && IsNamedType(items))
                s["items"] = Hoist(items, Identifier(name) + "Item");

        if (components["responses"] is JsonObject responses)
            foreach (var (name, response) in responses.OrderBy(r => r.Key, StringComparer.Ordinal))
                HoistContent(response as JsonObject,
                    name.StartsWith("response", StringComparison.OrdinalIgnoreCase) ? Identifier(name) : "Response" + Identifier(name));

        foreach (var (_, item) in document["paths"]!.AsObject())
        foreach (var operation in item!.AsObject().Select(o => o.Value).OfType<JsonObject>())
        {
            if (operation["operationId"]?.GetValue<string>() is not { } operationId)
                continue;
            var prefix = Identifier(operationId);

            if (operation["requestBody"] is JsonObject body)
                HoistContent(body, prefix + "Request");

            if (operation["responses"] is JsonObject operationResponses)
                foreach (var (status, response) in operationResponses)
                    HoistContent(response as JsonObject, status.StartsWith('2') ? prefix + "Response" : prefix + Identifier(status) + "Response");

            if (operation["parameters"] is JsonArray parameters)
                foreach (var parameter in parameters.OfType<JsonObject>().Where(p => p["schema"] is JsonObject s && s.ContainsKey("enum")))
                    parameter["schema"] = Hoist(parameter["schema"]!.AsObject(), prefix + Identifier(parameter["name"]!.GetValue<string>()));
        }

        if (hoisted > 0)
            Console.WriteLine($"  hoisted {hoisted} inline schema(s)");

        void HoistContent(JsonObject? owner, string name)
        {
            if (owner?["content"] is not JsonObject content)
                return;
            foreach (var media in content.Select(c => c.Value).OfType<JsonObject>())
            {
                if (media["schema"] is not JsonObject schema)
                    continue;
                if (schema["type"]?.GetValue<string>() == "array" && schema["items"] is JsonObject items && IsNamedType(items))
                    schema["items"] = Hoist(items, name + "Item");
                else if (IsNamedType(schema))
                    media["schema"] = Hoist(schema, name);
            }
        }

        JsonObject Hoist(JsonObject schema, string name)
        {
            var existing = schemas.FirstOrDefault(s => JsonNode.DeepEquals(s.Value, schema)).Key;
            if (existing is null)
            {
                var candidate = name;
                for (var i = 2; schemas.ContainsKey(candidate); i++)
                    candidate = name + i;
                schemas[candidate] = schema.DeepClone();
                existing = candidate;
                hoisted++;
            }
            return new JsonObject { ["$ref"] = "#/components/schemas/" + existing };
        }

        // a plain `type: string` stays inline; only what NSwag would turn into a class or enum gets a name
        static bool IsNamedType(JsonObject schema) =>
            !schema.ContainsKey("$ref")
            && (schema.ContainsKey("properties") || schema.ContainsKey("enum") || schema.ContainsKey("allOf")
                || schema.ContainsKey("oneOf") || schema.ContainsKey("anyOf"));

        static string Identifier(string value) =>
            string.Join("_", System.Text.RegularExpressions.Regex.Split(value, "[^A-Za-z0-9]+")
                .Where(s => s.Length > 0)
                .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }

    /// <summary>
    /// Forces every success response of the operation to a plain binary stream, for endpoints whose declared
    /// media type and schema disagree (Ozon labels: `application/pdf` with a JSON-object schema). NSwag then
    /// deterministically generates `Task&lt;FileResponse&gt;` and the caller decides what arrived by sniffing
    /// the bytes. Error responses stay as declared. Run before <see cref="SpecTrimmer.Trim"/> so the replaced
    /// schema drops out of the trim.
    /// </summary>
    public static void NormalizeBinaryResponse(string path, JsonObject operation)
    {
        if (operation["responses"] is not JsonObject responses)
            return;

        foreach (var (status, response) in responses)
        {
            if (!status.StartsWith('2') || response is not JsonObject { } r || r["content"] is not JsonObject content)
                continue;

            foreach (var (mediaType, _) in content.ToArray())
            {
                content[mediaType] = new JsonObject
                {
                    ["schema"] = new JsonObject { ["type"] = "string", ["format"] = "binary" },
                };
                Console.WriteLine($"  binary: {path} {status} {mediaType} -> string/binary");
            }
        }
    }
}
