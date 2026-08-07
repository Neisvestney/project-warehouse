#!/usr/bin/env dotnet
#:package NSwag.CodeGeneration.CSharp@14.*

// Trims the Ozon Seller API spec down to the paths we actually use and generates a C# client.
//
//   dotnet run tools/marketplaces/ozon/generate-client.cs -- --fetch   fetch, trim, generate
//   dotnet run tools/marketplaces/ozon/generate-client.cs              trim and generate from the raw file
//
// The `--` separator is required, otherwise dotnet swallows --fetch as its own argument.

using System.Text.Json;
using System.Text.Json.Nodes;
using NSwag;
using NSwag.CodeGeneration.CSharp;

const string SpecUrl = "https://docs.ozon.ru/api/seller/swagger.json";
const string BaseUrl = "https://api-seller.ozon.ru";

var here = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]) is { Length: > 0 } d && File.Exists(Path.Combine(d, "paths.whitelist.json"))
    ? d
    : AppContext.BaseDirectory;
if (!File.Exists(Path.Combine(here, "paths.whitelist.json")))
    here = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tools", "marketplaces", "ozon"));

var rawPath = Path.Combine(here, "ozon-swagger.raw.json");
var trimmedPath = Path.Combine(here, "ozon-openapi.trimmed.json");
var whitelistPath = Path.Combine(here, "paths.whitelist.json");
var outputPath = Path.GetFullPath(Path.Combine(here, "..", "..", "..",
    "ProjectWarehouse.Server", "Integrations", "Ozon", "Generated", "OzonApiClient.g.cs"));

if (args.Contains("--fetch"))
    await FetchAsync(rawPath);

if (!File.Exists(rawPath))
{
    Console.Error.WriteLine($"""
        Raw spec not found: {rawPath}

        Run with --fetch, or — if docs.ozon.ru refuses the request — open
        {SpecUrl}
        in a browser and save the response there manually.
        """);
    return 1;
}

Trim(rawPath, trimmedPath, whitelistPath);
await GenerateAsync(trimmedPath, outputPath);
return 0;

static async Task FetchAsync(string rawPath)
{
    Console.WriteLine($"Fetching {SpecUrl} ...");

    // docs.ozon.ru answers non-browser clients with a redirect loop (?__rr=N) or 403
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8");
    http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://docs.ozon.ru/api/seller/");

    var response = await http.GetAsync(SpecUrl);
    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}. Save the spec manually.");
        return;
    }

    var body = await response.Content.ReadAsStringAsync();
    if (!body.TrimStart().StartsWith('{'))
    {
        Console.Error.WriteLine("Fetch returned something that is not JSON (anti-bot page?). Save the spec manually.");
        return;
    }

    await File.WriteAllTextAsync(rawPath, body);
    Console.WriteLine($"Saved {body.Length / 1024} KB to {rawPath}");
}

static void Trim(string rawPath, string trimmedPath, string whitelistPath)
{
    var root = JsonNode.Parse(File.ReadAllText(rawPath))!.AsObject();
    var whitelistDocument = JsonNode.Parse(File.ReadAllText(whitelistPath))!.AsObject();
    var whitelist = whitelistDocument["include"]!
        .AsArray().Select(n => n!.GetValue<string>()).ToHashSet();
    var binaryResponses = whitelistDocument["binaryResponses"]?
        .AsArray().Select(n => n!.GetValue<string>()).ToHashSet() ?? [];

    var allPaths = root["paths"]!.AsObject();
    var keptPaths = new JsonObject();
    foreach (var path in whitelist)
    {
        if (!allPaths.TryGetPropertyValue(path, out var item))
            throw new InvalidOperationException($"Whitelisted path missing from the spec: {path}");
        keptPaths[path] = item!.DeepClone();
    }

    foreach (var operation in keptPaths.SelectMany(p => p.Value!.AsObject()).Select(o => o.Value).OfType<JsonObject>())
        StripCredentialParameters(operation);

    // Before CollectReachableComponents, so the replaced envelope schema drops out of the trim
    foreach (var path in binaryResponses)
        foreach (var operation in keptPaths[path]!.AsObject().Select(o => o.Value).OfType<JsonObject>())
            NormalizeBinaryResponse(path, operation);

    var reachable = CollectReachableComponents(keptPaths, root);

    var components = new JsonObject();
    foreach (var (section, names) in reachable.GroupBy(r => r.Section, r => r.Name)
                 .ToDictionary(g => g.Key, g => g.ToHashSet()))
    {
        var kept = new JsonObject();
        var source = root["components"]![section]!.AsObject();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
            kept[name] = source[name]!.DeepClone();
        components[section] = kept;
    }

    var trimmed = new JsonObject
    {
        ["openapi"] = root["openapi"]!.DeepClone(),
        ["info"] = root["info"]!.DeepClone(),
        // the spec ships "//api-seller.ozon.ru" (no scheme), which NSwag parses incorrectly
        ["servers"] = new JsonArray(new JsonObject { ["url"] = BaseUrl }),
        ["paths"] = keptPaths,
        ["components"] = components,
    };

    Sanitize(trimmed);
    FlattenDottedSchemaNames(trimmed);

    File.WriteAllText(trimmedPath, trimmed.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Trimmed {allPaths.Count} paths -> {keptPaths.Count}, " +
                      $"{new FileInfo(rawPath).Length / 1024} KB -> {new FileInfo(trimmedPath).Length / 1024} KB");
}

/// <summary>
/// The Ozon spec declares itself as OpenAPI 3 but keeps Swagger 2.0 leftovers NJsonSchema cannot read:
/// a boolean `required` on a property schema (3.0 wants an array of names on the owning object),
/// and array schemas that carry `items` without `"type": "array"` (silently generated as `object`).
/// </summary>
static void Sanitize(JsonNode node)
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

static void SanitizeSchema(JsonNode node)
{
    if (node is not JsonObject schema)
        return;

    if (schema["required"] is JsonValue required && required.TryGetValue<bool>(out _))
        schema.Remove("required");

    // Ozon writes prose into `type` in places (v3PostingProductDetail.jw_uin: "array of strings").
    // Printing every rewrite is deliberate: a silent fix is how the next such landmine gets missed.
    if (schema["type"] is JsonValue type && type.TryGetValue<string>(out var typeName)
        && !Oas.SchemaTypes.Contains(typeName))
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
/// Renames dotted component schemas (`posting.v4.…SortDir.Enum`) to a flat PascalCase name.
/// NJsonSchema types them by the last dot-segment, so every `….Enum` becomes `Enum` and NSwag pulls
/// them apart with positional suffixes — `Enum2`, `Products2` — that shift whenever the spec changes.
/// A generated type called `Enum` also shadows `System.Enum` inside the generated namespace.
/// </summary>
static void FlattenDottedSchemaNames(JsonObject trimmed)
{
    var schemas = trimmed["components"]?["schemas"]?.AsObject();
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
    trimmed["components"]!.AsObject()["schemas"] = renamed;

    RewriteReferences(trimmed, renames);
    Console.WriteLine($"  flattened {renames.Count} dotted schema name(s)");

    static bool IsNoise(string segment) =>
        segment.All(char.IsLower) || (segment.Length > 1 && segment[0] is 'v' && segment[1..].All(char.IsAsciiDigit));

    static string Pascal(string segment) =>
        segment.Length == 0 ? segment : char.ToUpperInvariant(segment[0]) + segment[1..];
}

static void RewriteReferences(JsonNode node, Dictionary<string, string> renames)
{
    switch (node)
    {
        case JsonObject o:
            foreach (var (key, value) in o.ToArray())
            {
                if (key == "$ref" && value is JsonValue v && v.GetValue<string>() is { } reference
                    && reference.StartsWith("#/components/schemas/", StringComparison.Ordinal)
                    && renames.TryGetValue(reference["#/components/schemas/".Length..], out var target))
                    o[key] = "#/components/schemas/" + target;
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

/// <summary>
/// Ozon declares the label response under `application/pdf` but hands it a JSON-object schema
/// (`file_content` / `file_name` / `content_type`). The two disagree and neither can be trusted, so
/// the success response is forced to a plain binary stream: NSwag then deterministically generates
/// `Task&lt;FileResponse&gt;` and the wrapper decides what actually arrived by sniffing the bytes.
/// Error responses stay JSON — they really are.
/// </summary>
static void NormalizeBinaryResponse(string path, JsonObject operation)
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

static void StripCredentialParameters(JsonObject operation)
{
    if (operation["parameters"] is not JsonArray parameters)
        return;

    var survivors = parameters
        .Where(p => !IsCredentialParameter(p))
        .Select(p => p!.DeepClone())
        .ToArray();

    if (survivors.Length == 0)
        operation.Remove("parameters");
    else
        operation["parameters"] = new JsonArray(survivors);

    static bool IsCredentialParameter(JsonNode? parameter)
    {
        if (parameter is not JsonObject o)
            return false;
        var name = o["$ref"]?.GetValue<string>()?.Split('/')[^1] ?? o["name"]?.GetValue<string>();
        return name is "Client-Id" or "Api-Key";
    }
}

static HashSet<(string Section, string Name)> CollectReachableComponents(JsonNode from, JsonObject root)
{
    var reachable = new HashSet<(string, string)>();
    var queue = new Queue<JsonNode>();
    queue.Enqueue(from);

    while (queue.Count > 0)
    {
        foreach (var reference in FindReferences(queue.Dequeue()))
        {
            var parts = reference.Split('/');
            if (parts is not ["#", "components", var section, var name])
                throw new InvalidOperationException($"Unsupported $ref shape: {reference}");
            if (!reachable.Add((section, name)))
                continue;

            var target = root["components"]?[section]?[name]
                ?? throw new InvalidOperationException($"Dangling $ref: {reference}");
            queue.Enqueue(target);
        }
    }

    return reachable;
}

static IEnumerable<string> FindReferences(JsonNode node)
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

static async Task GenerateAsync(string trimmedPath, string outputPath)
{
    var document = await OpenApiDocument.FromFileAsync(trimmedPath);

    var settings = new CSharpClientGeneratorSettings
    {
        ClassName = "OzonApiClient",
        OperationNameGenerator = new NSwag.CodeGeneration.OperationNameGenerators.SingleClientFromOperationIdOperationNameGenerator(),
        InjectHttpClient = true,
        DisposeHttpClient = false,
        UseBaseUrl = false,
        GenerateBaseUrlProperty = false,
        GenerateClientInterfaces = true,
        GenerateExceptionClasses = true,
        ExceptionClass = "OzonApiException",
        GenerateOptionalParameters = false,
        CSharpGeneratorSettings =
        {
            Namespace = "ProjectWarehouse.Server.Integrations.Ozon.Generated",
            GenerateNullableReferenceTypes = true,
            GenerateOptionalPropertiesAsNullable = true,
            RequiredPropertiesMustBeDefined = false,
            GenerateDataAnnotations = false,
            GenerateDefaultValues = false,
            ClassStyle = NJsonSchema.CodeGeneration.CSharp.CSharpClassStyle.Poco,
            JsonLibrary = NJsonSchema.CodeGeneration.CSharp.CSharpJsonLibrary.SystemTextJson,
            ArrayType = "System.Collections.Generic.IReadOnlyList",
            ArrayInstanceType = "System.Collections.Generic.List",
            DateTimeType = "System.DateTimeOffset",
        },
    };

    var code = new CSharpClientGenerator(document, settings).GenerateFile();

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, code);
    Console.WriteLine($"Generated {code.Length / 1024} KB -> {outputPath}");
}

static class Oas
{
    public static readonly HashSet<string> SchemaTypes =
        ["object", "array", "string", "integer", "number", "boolean", "null"];

    public static readonly HashSet<string> SchemaKeywords =
    [
        "$ref", "additionalProperties", "allOf", "anyOf", "default", "deprecated", "description",
        "discriminator", "enum", "example", "examples", "exclusiveMaximum", "exclusiveMinimum",
        "externalDocs", "format", "items", "maxItems", "maxLength", "maximum", "minItems", "minLength",
        "minimum", "not", "nullable", "oneOf", "pattern", "properties", "readOnly", "required", "title",
        "type", "uniqueItems", "writeOnly", "xml",
    ];
}
