#!/usr/bin/env dotnet
#:project ../../Generator/Neisvestney.Marketplaces.Generator/Neisvestney.Marketplaces.Generator.csproj

// Trims the Avito API specs down to the operations we actually use and generates a C# client.
//
//   dotnet run src/Marketplaces/Avito/generator/generate-client.cs     trim and generate from specs/avito/
//
// There is no --fetch: the Avito portal answers scripted requests with 403/429.

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Neisvestney.Marketplaces.Generator;

const string Namespace = "Neisvestney.Marketplaces.Avito";

var here = ScriptDirectory();
var rawDirectory = Path.GetFullPath(Path.Combine(here, "..", "..", "specs", "avito"));
var trimmedPath = Path.Combine(here, "avito-openapi.trimmed.json");
var whitelistPath = Path.Combine(here, "paths.whitelist.json");
var generatedDirectory = Path.GetFullPath(Path.Combine(here, "..", "Neisvestney.Marketplaces.Avito", "Generated"));

var sources = SpecLoader.LoadJson(whitelistPath)["sources"]!.AsObject()
    .Select(s => (Name: s.Key, Entries: s.Value!.AsArray().Select(e => e!.GetValue<string>()).ToList()))
    .ToList();

var missing = sources.Select(s => RawPath(rawDirectory, s.Name)).Where(p => !File.Exists(p)).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine($"""
        Raw spec not found: {string.Join(", ", missing)}

        The Avito portal blocks scripted downloads (anti-bot 403/429). Export the specs by hand:
        open https://www.avito.ru/developers/api-catalog in a browser, then in that tab's devtools console run
        fetch('/web/1/openapi/info/<slug>') for each section and save the response's `swagger` field
        as src/Marketplaces/specs/avito/<slug>.json (e.g. specs/avito/order-management.json).
        """);
    return 1;
}

var baseUrl = Trim(rawDirectory, sources, trimmedPath);
await ClientGenerator.GenerateAsync(trimmedPath,
    ClientGenerator.DefaultSettings(Namespace, "AvitoApiClient", "AvitoApiException"),
    Path.Combine(generatedDirectory, "AvitoApiClient.g.cs"),
    enumConverter: "global::Neisvestney.Marketplaces.Shared.TolerantEnumConverter");
WriteHosts(baseUrl, Path.Combine(generatedDirectory, "AvitoHosts.g.cs"));
return 0;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static string RawPath(string rawDirectory, string source) => Path.Combine(rawDirectory, source + ".json");

/// <summary>Returns the base URL every section shares.</summary>
static string Trim(string rawDirectory, List<(string Name, List<string> Entries)> sources, string trimmedPath)
{
    var parts = new List<(string Prefix, TrimmedSpec Spec)>();
    string? baseUrl = null;
    JsonObject? first = null;

    foreach (var (name, entries) in sources)
    {
        var root = SpecLoader.LoadJson(RawPath(rawDirectory, name));
        first ??= root;

        var server = root["servers"]?.AsArray().Select(s => s!["url"]!.GetValue<string>()).SingleOrDefault()
            ?? throw new InvalidOperationException($"{name} declares no single server");
        server = server.TrimEnd('/') + "/";
        if (baseUrl is not null && baseUrl != server)
            throw new InvalidOperationException($"{name} is served from {server}, not {baseUrl}; split the client by host");
        baseUrl = server;

        // Avito terms forbid copying the documentation; stripped per section so prose does not split
        // components that the merge would otherwise collapse (every section words its 401 differently)
        SpecSanitizer.StripDocumentation(root);
        CleanPathKeys(root, name);
        var paths = SpecTrimmer.SelectPaths(root, entries);
        // the token goes into a header set by AvitoAuthHandler, never a method argument
        SpecTrimmer.StripParameters(paths, n => n is "Authorization" or "authHeader");

        foreach (var (path, method, operation) in SpecTrimmer.Operations(paths))
        {
            operation.Remove("security");
            if (operation["operationId"] is null)
            {
                operation["operationId"] = SynthesizeOperationId(method, path);
                Console.WriteLine($"  operationId: {method.ToUpperInvariant()} {path} -> {operation["operationId"]}");
            }
        }

        parts.Add((SourcePrefix(name), SpecTrimmer.Trim(root, paths)));
    }

    var merged = SpecTrimmer.Merge(parts);
    var duplicate = SpecTrimmer.Operations(merged.Paths).GroupBy(o => o.Operation["operationId"]!.GetValue<string>()).FirstOrDefault(g => g.Count() > 1);
    if (duplicate is not null)
        throw new InvalidOperationException($"operationId {duplicate.Key} is used by several operations");

    var document = new JsonObject
    {
        ["openapi"] = first!["openapi"]!.DeepClone(),
        ["info"] = new JsonObject { ["title"] = "Avito API", ["version"] = "1.0" },
        ["servers"] = new JsonArray(new JsonObject { ["url"] = baseUrl }),
        ["paths"] = merged.Paths,
        ["components"] = merged.Components,
    };

    StripEnums(document);
    FixHomoglyphPropertyNames(document);
    WidenIntegers(document);
    SpecSanitizer.NameInlineSchemas(document);
    NameInlinePropertySchemas(document);
    SpecSanitizer.Sanitize(document);
    SpecSanitizer.FlattenDottedSchemaNames(document);

    SpecLoader.Save(document, trimmedPath);
    Console.WriteLine($"Trimmed {sources.Count} sources -> {merged.Paths.Count} paths, {new FileInfo(trimmedPath).Length / 1024} KB");
    return baseUrl!;
}

// The auth section tells its three POST /token operations apart by appending U+200E to the path key
static void CleanPathKeys(JsonObject root, string source)
{
    var paths = root["paths"]!.AsObject();
    var dirty = paths.Select(p => p.Key).Where(k => k.Any(IsInvisible)).ToList();
    foreach (var key in dirty)
    {
        var clean = new string(key.Where(c => !IsInvisible(c)).ToArray());
        if (paths.ContainsKey(clean))
        {
            Console.WriteLine($"  path: {source} {Escape(key)} collides with {clean} once cleaned, dropped");
            paths.Remove(key);
            continue;
        }
        var item = paths[key];
        paths.Remove(key);
        paths[clean] = item;
        Console.WriteLine($"  path: {source} {Escape(key)} -> {clean}");
    }

    static bool IsInvisible(char c) => char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.Format;

    static string Escape(string value) =>
        string.Concat(value.Select(c => IsInvisible(c) ? $"\\u{(int)c:X4}" : c.ToString()));
}

// "POST /stock-management/1/info" -> "postStockManagementInfo"; version segments carry no meaning
static string SynthesizeOperationId(string method, string path)
{
    var words = path.Split('/', '-', '_', '{', '}')
        .Where(s => s.Length > 0 && !s.All(char.IsAsciiDigit))
        .Select(s => char.ToUpperInvariant(s[0]) + s[1..]);
    return method.ToLowerInvariant() + string.Concat(words);
}

// Avito enums are incomplete and drift from what the API returns; a plain string never fails a page
static void StripEnums(JsonNode node)
{
    var count = 0;
    Walk(node);
    Console.WriteLine($"  enums: {count} schema(s) rewritten to plain values");

    void Walk(JsonNode? current)
    {
        switch (current)
        {
            case JsonObject o:
                // an array value tells the keyword apart from a property named "enum", whose value is a schema
                if (o["enum"] is JsonArray values)
                {
                    o.Remove("enum");
                    foreach (var key in o.Select(p => p.Key).Where(k => k.StartsWith("x-enum", StringComparison.OrdinalIgnoreCase)).ToList())
                        o.Remove(key);
                    if (!o.ContainsKey("type"))
                        o["type"] = values.All(v => v is JsonValue j && j.TryGetValue<long>(out _)) ? "integer" : "string";
                    count++;
                }
                foreach (var (_, value) in o.ToArray())
                    Walk(value);
                break;
            case JsonArray a:
                foreach (var item in a)
                    Walk(item);
                break;
        }
    }
}

// Item ids already pass int.MaxValue while the spec declares them int32 or with no format; a long costs nothing
static void WidenIntegers(JsonNode node)
{
    var count = 0;
    Walk(node);
    Console.WriteLine($"  integers: {count} schema(s) widened to int64");

    void Walk(JsonNode? current)
    {
        switch (current)
        {
            case JsonObject o:
                if (o["type"] is JsonValue type && type.TryGetValue<string>(out var name) && name == "integer"
                    && o["format"]?.GetValue<string>() != "int64")
                {
                    o["format"] = "int64";
                    count++;
                }
                foreach (var (_, value) in o.ToArray())
                    Walk(value);
                break;
            case JsonArray a:
                foreach (var item in a)
                    Walk(item);
                break;
        }
    }
}

// schedules.deliveryDateMax is spelled with a Cyrillic U+0445; the API sends the Latin name, like deliveryDateMin
static void FixHomoglyphPropertyNames(JsonNode node)
{
    const string cyrillic = "\u0430\u0435\u043E\u0440\u0441\u0443\u0445\u0410\u0412\u0415\u041A\u041C\u041D\u041E\u0420\u0421\u0422\u0425";
    const string latin = "aeopcyxABEKMHOPCTX";
    switch (node)
    {
        case JsonObject o:
            if (o["properties"] is JsonObject properties)
                foreach (var key in properties.Select(p => p.Key).Where(k => k.Any(c => c > 127)).ToList())
                {
                    var fixedKey = string.Concat(key.Select(c => cyrillic.IndexOf(c) is var i and >= 0 ? latin[i] : c));
                    if (fixedKey.Any(c => c > 127) || properties.ContainsKey(fixedKey))
                        throw new InvalidOperationException($"Property {key} is not an ASCII name with a homoglyph typo; handle it here");
                    var value = properties[key];
                    properties.Remove(key);
                    properties[fixedKey] = value;
                    if (o["required"] is JsonArray required)
                        foreach (var entry in required.OfType<JsonValue>().Where(r => r.GetValue<string>() == key).ToList())
                            required[required.IndexOf(entry)] = fixedKey;
                    Console.WriteLine($"  property: {Escape(key)} -> {fixedKey}");
                }
            foreach (var (_, value) in o.ToArray())
                if (value is not null)
                    FixHomoglyphPropertyNames(value);
            break;
        case JsonArray a:
            foreach (var item in a.OfType<JsonNode>())
                FixHomoglyphPropertyNames(item);
            break;
    }

    static string Escape(string value) => string.Concat(value.Select(c => c > 127 ? $"\\u{(int)c:X4}" : c.ToString()));
}

// Nested inline objects (four error schemas each with an inline `error`) would get NJsonSchema's positional
// Error2..Error5; hoisting them as {Owner}{Property} keeps names stable across spec updates
static void NameInlinePropertySchemas(JsonObject document)
{
    var schemas = document["components"]!["schemas"]!.AsObject();
    var hoisted = 0;
    var queue = new Queue<string>(schemas.Select(s => s.Key).Order(StringComparer.Ordinal));
    while (queue.Count > 0)
    {
        var owner = queue.Dequeue();
        if (schemas[owner]?["properties"] is not JsonObject properties)
            continue;
        foreach (var (property, value) in properties.ToArray())
        {
            if (value is not JsonObject schema)
                continue;
            var name = owner + Pascal(property);
            if (IsInlineObject(schema))
                properties[property] = Hoist(schema, name);
            else if (schema["items"] is JsonObject items && IsInlineObject(items))
                schema["items"] = Hoist(items, name + "Item");
        }
    }
    if (hoisted > 0)
        Console.WriteLine($"  hoisted {hoisted} nested property schema(s)");

    JsonObject Hoist(JsonObject schema, string name)
    {
        var existing = schemas.FirstOrDefault(s => JsonNode.DeepEquals(s.Value, schema)).Key;
        if (existing is null)
        {
            existing = name;
            for (var i = 2; schemas.ContainsKey(existing); i++)
                existing = name + i;
            schemas[existing] = schema.DeepClone();
            queue.Enqueue(existing);
            hoisted++;
        }
        return new JsonObject { ["$ref"] = "#/components/schemas/" + existing };
    }

    static bool IsInlineObject(JsonObject schema) =>
        !schema.ContainsKey("$ref") && (schema.ContainsKey("properties") || schema.ContainsKey("allOf"));

    static string Pascal(string value) =>
        string.Concat(value.Split('_', '-', '.').Where(s => s.Length > 0).Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
}

// "order-management" -> "OrderManagement"; prefixes component names that collide across sections
static string SourcePrefix(string source) =>
    string.Concat(source.Split('-').Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

static void WriteHosts(string baseUrl, string outputPath)
{
    var code = new StringBuilder();
    code.AppendLine("// <auto-generated>Generated by generator/generate-client.cs — do not edit by hand.</auto-generated>");
    code.AppendLine($"namespace {Namespace};");
    code.AppendLine();
    code.AppendLine("/// <summary>Base address of <see cref=\"AvitoApiClient\"/>; every Avito API section shares it.</summary>");
    code.AppendLine("public static class AvitoHosts");
    code.AppendLine("{");
    code.AppendLine($"    public static readonly Uri Api = new(\"{baseUrl}\");");
    code.AppendLine("}");

    File.WriteAllText(outputPath, code.ToString());
    Console.WriteLine($"Generated hosts -> {outputPath}");
}
