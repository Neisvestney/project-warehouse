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
    var whitelist = JsonNode.Parse(File.ReadAllText(whitelistPath))!.AsObject()["include"]!
        .AsArray().Select(n => n!.GetValue<string>()).ToHashSet();

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
    public static readonly HashSet<string> SchemaKeywords =
    [
        "$ref", "additionalProperties", "allOf", "anyOf", "default", "deprecated", "description",
        "discriminator", "enum", "example", "examples", "exclusiveMaximum", "exclusiveMinimum",
        "externalDocs", "format", "items", "maxItems", "maxLength", "maximum", "minItems", "minLength",
        "minimum", "not", "nullable", "oneOf", "pattern", "properties", "readOnly", "required", "title",
        "type", "uniqueItems", "writeOnly", "xml",
    ];
}
