#!/usr/bin/env dotnet
#:project ../../Generator/Neisvestney.Marketplaces.Generator/Neisvestney.Marketplaces.Generator.csproj

// Trims the Yandex Market Partner API spec down to the operations we actually use and generates a C# client.
//
//   dotnet run src/Marketplaces/YandexMarket/generator/generate-client.cs -- --fetch   fetch, trim, generate
//   dotnet run src/Marketplaces/YandexMarket/generator/generate-client.cs              trim and generate from specs/yandex-market/
//
// The `--` separator is required, otherwise dotnet swallows --fetch as its own argument.
//
// The spec (github.com/yandex-market/yandex-market-partner-api) is BSD-3-Clause, Copyright (c) 2023 Yandex Market LLC;
// the license text sits next to this script in LICENSE. Unlike the other marketplaces, its documentation may be
// redistributed, so descriptions stay in the trimmed spec and the generated client, and both are committed.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Neisvestney.Marketplaces.Generator;

const string ArchiveUrl = "https://github.com/yandex-market/yandex-market-partner-api/archive/refs/heads/main.zip";
const string BaseUrl = "https://api.partner.market.yandex.ru";

var here = ScriptDirectory();
var repositoryDirectory = Path.GetFullPath(Path.Combine(here, "..", "..", "specs", "yandex-market"));
var specDirectory = Path.Combine(repositoryDirectory, "openapi");
var trimmedPath = Path.Combine(here, "yandex-market-openapi.trimmed.json");
var whitelistPath = Path.Combine(here, "paths.whitelist.json");
var outputPath = Path.GetFullPath(Path.Combine(here, "..",
    "Neisvestney.Marketplaces.YandexMarket", "Generated", "YandexMarketApiClient.g.cs"));

if (args.Contains("--fetch"))
    await FetchAsync(repositoryDirectory);

if (!File.Exists(Path.Combine(specDirectory, "openapi.yaml")))
{
    Console.Error.WriteLine($"Raw spec not found under {specDirectory}. Run with --fetch.");
    return 1;
}

// BSD-3 requires the notice to travel with the redistributed (committed) trimmed spec
var licensePath = Path.Combine(repositoryDirectory, "LICENSE");
if (!File.Exists(licensePath))
{
    Console.Error.WriteLine($"{licensePath} not found: the trimmed spec cannot be redistributed without it. Run with --fetch.");
    return 1;
}
File.Copy(licensePath, Path.Combine(here, "LICENSE"), overwrite: true);
Trim(specDirectory, trimmedPath, whitelistPath);
await ClientGenerator.GenerateAsync(trimmedPath, Settings(), outputPath,
    enumConverter: "global::Neisvestney.Marketplaces.Shared.TolerantEnumConverter");
PrependLicenseHeader(outputPath, licensePath);
return 0;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

// The XML docs are the spec's prose, so the generated file is a source redistribution and must retain the notice
static void PrependLicenseHeader(string outputPath, string licensePath)
{
    string[] header =
    [
        "// Documentation comments in this file are taken from the Yandex Market Partner API specification",
        "// (https://github.com/yandex-market/yandex-market-partner-api) and are distributed under its license:",
        "//",
        .. File.ReadAllLines(licensePath).Select(line => ("// " + line).TrimEnd()),
    ];
    File.WriteAllText(outputPath, string.Join('\n', header) + "\n\n" + File.ReadAllText(outputPath));
}

static NSwag.CodeGeneration.CSharp.CSharpClientGeneratorSettings Settings()
{
    var settings = ClientGenerator.DefaultSettings("Neisvestney.Marketplaces.YandexMarket", "YandexMarketApiClient", "YandexMarketApiException");
    // `format: time` is a time of day ("09:00"); TimeOnlyConverter reads it, the built-in parser demands seconds
    settings.CSharpGeneratorSettings.TimeType = "System.TimeOnly";
    return settings;
}

static async Task FetchAsync(string repositoryDirectory)
{
    Console.WriteLine($"Fetching {ArchiveUrl} ...");
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    var archive = await http.GetByteArrayAsync(ArchiveUrl);

    // unpacked next to the old tree and swapped in only when complete, so a failed extraction leaves the old spec intact
    var staging = repositoryDirectory + ".staging";
    if (Directory.Exists(staging))
        Directory.Delete(staging, recursive: true);

    // entries sit under "<repo>-main/"; unpacked without that root, so the tree is specs/yandex-market/openapi/…
    var root = staging + Path.DirectorySeparatorChar;
    using (var zip = new ZipArchive(new MemoryStream(archive)))
        foreach (var entry in zip.Entries)
        {
            var relative = entry.FullName[(entry.FullName.IndexOf('/') + 1)..];
            if (relative.Length == 0 || relative.EndsWith('/'))
                continue;
            var target = Path.GetFullPath(Path.Combine(staging, relative));
            if (!target.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidOperationException($"Archive entry escapes the spec directory: {entry.FullName}");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }

    // replaced whole: a stale tree would keep schema files the upstream has since deleted
    if (Directory.Exists(repositoryDirectory))
        Directory.Delete(repositoryDirectory, recursive: true);
    Directory.Move(staging, repositoryDirectory);
    Console.WriteLine($"Saved {archive.Length / 1024} KB -> {repositoryDirectory}");
}

static void Trim(string specDirectory, string trimmedPath, string whitelistPath)
{
    var root = Bundle(specDirectory);
    var whitelist = SpecLoader.LoadJson(whitelistPath);
    var include = whitelist["include"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
    var binaryResponses = whitelist["binaryResponses"]?.AsArray().Select(n => n!.GetValue<string>()).ToHashSet() ?? [];

    var paths = SpecTrimmer.SelectPaths(root, include);
    // the key goes into a header set by YandexMarketAuthHandler, never a method argument
    SpecTrimmer.StripParameters(paths, name => name is "Api-Key" or "Authorization");
    DropReplacedParameters(root, paths);

    foreach (var (path, method, operation) in SpecTrimmer.Operations(paths))
    {
        // YM reads are GETs or POSTs named get*; a write slipping into the whitelist must be a deliberate edit here
        var operationId = operation["operationId"]?.GetValue<string>() ?? "";
        if (method != "get" && !operationId.StartsWith("get", StringComparison.Ordinal))
            throw new InvalidOperationException($"{method.ToUpperInvariant()} {path} ({operationId}) does not look like a read");
        if (binaryResponses.Contains($"{method.ToUpperInvariant()} {path}"))
            SpecSanitizer.NormalizeBinaryResponse(path, operation);
    }

    var trimmed = SpecTrimmer.Trim(root, paths);
    var document = new JsonObject
    {
        ["openapi"] = root["openapi"]!.DeepClone(),
        ["info"] = WithLicense(root["info"]!.DeepClone().AsObject()),
        ["servers"] = new JsonArray(new JsonObject { ["url"] = BaseUrl }),
        ["paths"] = trimmed.Paths,
        ["components"] = trimmed.Components,
    };

    // no StripDocumentation: the spec is BSD-3-Clause, its prose may be redistributed
    StripTemplateMarkup(document);
    SpecSanitizer.NameInlineSchemas(document);
    SpecSanitizer.Sanitize(document);
    SpecSanitizer.FlattenDottedSchemaNames(document);

    // the spec is committed with its Russian prose; SpecLoader.Save would escape every Cyrillic letter to \uXXXX
    File.WriteAllText(trimmedPath, document.ToJsonString(new JsonSerializerOptions(SpecLoader.WriteOptions)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
    Console.WriteLine($"Trimmed {root["paths"]!.AsObject().Count} paths -> {paths.Count}, {new FileInfo(trimmedPath).Length / 1024} KB");
}

// JSON has no comments, so the committed spec carries its license in the standard info.license field
static JsonObject WithLicense(JsonObject info)
{
    info["license"] = new JsonObject
    {
        ["name"] = "BSD-3-Clause, Copyright (c) 2023, Yandex Market LLC",
        ["url"] = "https://github.com/yandex-market/yandex-market-partner-api/blob/main/LICENSE",
    };
    return info;
}

// A deprecated parameter whose replacement is declared alongside (from_date next to fromDate) would give the method two
// arguments for one filter; it is dropped only when x-deprecation-config names a replacement that is actually present.
static void DropReplacedParameters(JsonObject root, JsonObject paths)
{
    var components = root["components"]?["parameters"] as JsonObject;
    var dropped = 0;

    foreach (var (path, method, operation) in SpecTrimmer.Operations(paths))
    {
        if (operation["parameters"] is not JsonArray parameters)
            continue;

        var resolved = parameters.Select(p => (Node: p!, Parameter: Resolve(p!))).ToList();
        var names = resolved.Select(p => p.Parameter?["name"]?.GetValue<string>()).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var survivors = resolved
            .Where(p => !(p.Parameter?["deprecated"]?.GetValue<bool>() == true
                          && p.Parameter["x-deprecation-config"]?["replacement-field"]?.GetValue<string>() is { } replacement
                          && names.Contains(replacement)))
            .Select(p => p.Node.DeepClone())
            .ToArray();

        if (survivors.Length == parameters.Count)
            continue;
        dropped += parameters.Count - survivors.Length;
        operation["parameters"] = new JsonArray(survivors);
    }

    if (dropped > 0)
        Console.WriteLine($"  deprecated: {dropped} parameter(s) with a declared replacement dropped");

    JsonObject? Resolve(JsonNode parameter) =>
        parameter["$ref"]?.GetValue<string>() is { } reference
            ? components?[reference.Split('/')[^1]] as JsonObject
            : parameter as JsonObject;
}

// Descriptions carry the docs site's template tags ({% note %}, {% include … %}, {% if … %}), meaningless in XML docs.
// A note or cut keeps its quoted title as plain text; every other tag is removed and its body stays.
static void StripTemplateMarkup(JsonNode node)
{
    switch (node)
    {
        case JsonObject o:
            foreach (var (key, value) in o.ToArray())
            {
                if (key is "description" or "summary" && value is JsonValue v && v.TryGetValue<string>(out var text) && text.Contains("{%"))
                    o[key] = CleanMarkup(text);
                else if (value is not null)
                    StripTemplateMarkup(value);
            }
            break;
        case JsonArray a:
            foreach (var item in a.OfType<JsonNode>())
                StripTemplateMarkup(item);
            break;
    }

    static string CleanMarkup(string text)
    {
        var cleaned = Regex.Replace(text, """\{%\s*(?:note|cut)\b[^"%]*(?:"(?<title>[^"]*)")?[^%]*%\}""", m => m.Groups["title"].Value);
        // Singleline: a long {% include … %} may wrap onto the next line
        cleaned = Regex.Replace(cleaned, @"\{%.*?%\}", "", RegexOptions.Singleline);
        cleaned = Regex.Replace(cleaned, @"[ \t]+\n", "\n");
        return Regex.Replace(cleaned, @"\n{3,}", "\n\n").Trim();
    }
}

// The upstream spec is split into one file per path item and per component (components/<section>/<Name>.yaml)
// linked by relative $refs. Inlines the path items and turns every component file into #/components/<section>/<Name>.
static JsonObject Bundle(string specDirectory)
{
    var componentsDirectory = Path.GetFullPath(Path.Combine(specDirectory, "components"));
    var root = SpecLoader.LoadYaml(Path.Combine(specDirectory, "openapi.yaml"));
    var components = new SortedDictionary<string, SortedDictionary<string, JsonNode>>(StringComparer.Ordinal);
    var pending = new Queue<(string Section, string Name, string File)>();

    var paths = root["paths"]!.AsObject();
    foreach (var (path, item) in paths.ToArray())
    {
        if (item?["$ref"]?.GetValue<string>() is not { } reference)
            continue;
        var file = Path.GetFullPath(Path.Combine(specDirectory, reference));
        var body = SpecLoader.LoadYaml(file);
        RewriteReferences(body, Path.GetDirectoryName(file)!);
        paths[path] = body;
    }

    while (pending.TryDequeue(out var component))
    {
        var section = components.TryGetValue(component.Section, out var s) ? s : components[component.Section] = new(StringComparer.Ordinal);
        if (section.ContainsKey(component.Name))
            continue;
        var body = SpecLoader.LoadYaml(component.File);
        section[component.Name] = body;
        RewriteReferences(body, Path.GetDirectoryName(component.File)!);
    }

    root["components"] = new JsonObject(components.Select(section => KeyValuePair.Create<string, JsonNode?>(section.Key,
        new JsonObject(section.Value.Select(c => KeyValuePair.Create<string, JsonNode?>(c.Key, c.Value))))));
    Console.WriteLine($"Bundled {paths.Count} paths, {components.Sum(c => c.Value.Count)} components");
    return root;

    void RewriteReferences(JsonNode node, string directory)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o.ToArray())
                {
                    if (key == "$ref" && value is JsonValue v && v.GetValue<string>() is var reference && !reference.StartsWith('#'))
                        o[key] = Resolve(reference, directory);
                    else if (value is not null)
                        RewriteReferences(value, directory);
                }
                break;
            case JsonArray a:
                foreach (var item in a.OfType<JsonNode>())
                    RewriteReferences(item, directory);
                break;
        }
    }

    string Resolve(string reference, string directory)
    {
        if (reference.Contains('#'))
            throw new InvalidOperationException($"Unsupported $ref with a fragment: {reference}");
        var file = Path.GetFullPath(Path.Combine(directory, reference));
        var relative = Path.GetRelativePath(componentsDirectory, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relative is not [var section, var fileName] || section == ".." || Path.GetExtension(fileName) != ".yaml")
            throw new InvalidOperationException($"$ref {reference} from {directory} does not point at components/<section>/<Name>.yaml");
        var name = Path.GetFileNameWithoutExtension(fileName);
        pending.Enqueue((section, name, file));
        return $"#/components/{section}/{name}";
    }
}
