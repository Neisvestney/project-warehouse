#!/usr/bin/env dotnet
#:project ../../Generator/Neisvestney.Marketplaces.Generator/Neisvestney.Marketplaces.Generator.csproj

// Trims the Wildberries API specs down to the operations we actually use and generates C# clients,
// one per API host.
//
//   dotnet run src/Marketplaces/Wildberries/generator/generate-client.cs -- --fetch   fetch, trim, generate
//   dotnet run src/Marketplaces/Wildberries/generator/generate-client.cs              trim and generate from raw/
//
// The `--` separator is required, otherwise dotnet swallows --fetch as its own argument.

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Neisvestney.Marketplaces.Generator;
using NSwag.CodeGeneration.OperationNameGenerators;

const string SpecUrl = "https://dev.wildberries.ru/api/swagger/yaml/ru/{0}.yaml";
const string Namespace = "Neisvestney.Marketplaces.Wildberries";

var here = ScriptDirectory();
var rawDirectory = Path.Combine(here, "raw");
var trimmedPath = Path.Combine(here, "wildberries-openapi.trimmed.json");
var whitelistPath = Path.Combine(here, "paths.whitelist.json");
var generatedDirectory = Path.GetFullPath(Path.Combine(here, "..", "Neisvestney.Marketplaces.Wildberries", "Generated"));

var sources = SpecLoader.LoadJson(whitelistPath)["sources"]!.AsObject()
    .Select(s => (Name: s.Key, Entries: s.Value!.AsArray().Select(e => e!.GetValue<string>()).ToList()))
    .ToList();

if (args.Contains("--fetch"))
    await FetchAsync(rawDirectory, sources.Select(s => s.Name));

var missing = sources.Select(s => RawPath(rawDirectory, s.Name)).Where(p => !File.Exists(p)).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine($"Raw spec not found: {string.Join(", ", missing)}. Run with --fetch.");
    return 1;
}

var hosts = Trim(rawDirectory, sources, trimmedPath);
await ClientGenerator.GenerateAsync(trimmedPath, Settings(), Path.Combine(generatedDirectory, "WildberriesClients.g.cs"),
    enumConverter: "global::Neisvestney.Marketplaces.Shared.TolerantEnumConverter");
WriteHosts(hosts, Path.Combine(generatedDirectory, "WildberriesHosts.g.cs"));
WriteHooks(hosts.Keys, Path.Combine(generatedDirectory, "WildberriesClientHooks.g.cs"));
return 0;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

static string RawPath(string rawDirectory, string source) => Path.Combine(rawDirectory, source + ".yaml");

static async Task FetchAsync(string rawDirectory, IEnumerable<string> sources)
{
    Directory.CreateDirectory(rawDirectory);
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    foreach (var source in sources)
    {
        var url = string.Format(SpecUrl, source);
        Console.WriteLine($"Fetching {url} ...");
        var body = await http.GetStringAsync(url);
        if (!body.TrimStart().StartsWith("openapi:", StringComparison.Ordinal))
            throw new InvalidOperationException($"{url} returned something that is not an OpenAPI YAML (anti-bot page?)");
        await File.WriteAllTextAsync(RawPath(rawDirectory, source), body);
    }
}

/// <summary>Returns production and sandbox base URLs per client group.</summary>
static SortedDictionary<string, (string Production, string? Sandbox)> Trim(
    string rawDirectory, List<(string Name, List<string> Entries)> sources, string trimmedPath)
{
    var hosts = new SortedDictionary<string, (string, string?)>(StringComparer.Ordinal);
    var parts = new List<(string Prefix, TrimmedSpec Spec)>();
    JsonObject? first = null;

    foreach (var (name, entries) in sources)
    {
        var root = SpecLoader.LoadYaml(RawPath(rawDirectory, name));
        first ??= root;

        var paths = SpecTrimmer.SelectPaths(root, entries);
        // the token goes into a header set by WildberriesAuthHandler, never a method argument
        SpecTrimmer.StripParameters(paths, n => n is "Authorization");

        foreach (var (path, item) in paths)
            TagByHost(path, item!.AsObject(), hosts);

        foreach (var (path, method, operation) in SpecTrimmer.Operations(paths))
        {
            // a write slipping into the whitelist must be a deliberate edit here
            var readOnly = operation["x-readonly-method"] is JsonValue flag && flag.TryGetValue<bool>(out var value) ? value : (bool?)null;
            if (readOnly is false)
                throw new InvalidOperationException($"{method.ToUpperInvariant()} {path} is not read-only");
            if (readOnly is null)
                Console.WriteLine($"  warning: {method.ToUpperInvariant()} {path} has no x-readonly-method flag, check it is a read");
        }

        parts.Add((SourcePrefix(name), SpecTrimmer.Trim(root, paths)));
    }

    var merged = SpecTrimmer.Merge(parts);
    var document = new JsonObject
    {
        ["openapi"] = first!["openapi"]!.DeepClone(),
        ["info"] = new JsonObject { ["title"] = "Wildberries API", ["version"] = "1.0" },
        ["paths"] = merged.Paths,
        ["components"] = merged.Components,
    };

    // WB terms forbid copying the documentation; stripped before hoisting so prose does not split identical schemas
    SpecSanitizer.StripDocumentation(document);
    SpecSanitizer.NameInlineSchemas(document);
    SpecSanitizer.Sanitize(document);
    SpecSanitizer.FlattenDottedSchemaNames(document);

    SpecLoader.Save(document, trimmedPath);
    Console.WriteLine($"Trimmed {sources.Count} sources -> {merged.Paths.Count} paths, {new FileInfo(trimmedPath).Length / 1024} KB");
    return hosts;
}

// Every WB path declares its own servers; the host decides the client class, so a caller sets one base address per client
static void TagByHost(string path, JsonObject item, SortedDictionary<string, (string Production, string? Sandbox)> hosts)
{
    var urls = item["servers"]?.AsArray().Select(s => s!["url"]!.GetValue<string>()).ToList()
        ?? throw new InvalidOperationException($"{path} declares no servers");
    item.Remove("servers");

    var production = urls.FirstOrDefault(u => !u.Contains("-sandbox."))
        ?? throw new InvalidOperationException($"{path} has no production server");
    if (!Uri.TryCreate(production, UriKind.Absolute, out var uri))
        throw new InvalidOperationException($"{path}: server {production} is not an absolute URL");
    var host = uri.Host;
    var apiSuffix = host.IndexOf("-api.", StringComparison.Ordinal);
    if (apiSuffix <= 0)
        throw new InvalidOperationException($"{path}: host {host} does not follow <group>-api.wildberries.ru, name its client group here");
    var group = string.Concat(host[..apiSuffix].Split('-', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    var sandbox = urls.FirstOrDefault(u => u.Contains(host.Replace("-api.", "-api-sandbox.")));

    if (hosts.TryGetValue(group, out var known) && known.Production != production)
        throw new InvalidOperationException($"Client group {group} maps to both {known.Production} and {production}");
    hosts[group] = (production, known.Sandbox ?? sandbox);

    foreach (var operation in item.Select(o => o.Value).OfType<JsonObject>())
        operation["tags"] = new JsonArray(group);
}

// "03-orders-fbs" -> "OrdersFbs"; prefixes component names that collide across sections
static string SourcePrefix(string source) =>
    string.Concat(source.Split('-').SkipWhile(s => s.All(char.IsAsciiDigit)).Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

static NSwag.CodeGeneration.CSharp.CSharpClientGeneratorSettings Settings()
{
    var settings = ClientGenerator.DefaultSettings(Namespace, "Wildberries{controller}Client", "WildberriesApiException");
    settings.OperationNameGenerator = new MultipleClientsFromFirstTagAndOperationIdGenerator();
    return settings;
}

static void WriteHosts(SortedDictionary<string, (string Production, string? Sandbox)> hosts, string outputPath)
{
    var code = new StringBuilder();
    code.AppendLine("// <auto-generated>Generated by generator/generate-client.cs — do not edit by hand.</auto-generated>");
    code.AppendLine($"namespace {Namespace};");
    code.AppendLine();
    code.AppendLine("/// <summary>Base address of each generated client: <c>Wildberries{Group}Client</c> talks to <c>{Group}</c>.</summary>");
    code.AppendLine("public static class WildberriesHosts");
    code.AppendLine("{");
    foreach (var (group, (production, sandbox)) in hosts)
    {
        code.AppendLine($"    public static readonly Uri {group} = new(\"{production}\");");
        if (sandbox is not null)
            code.AppendLine($"    public static readonly Uri {group}Sandbox = new(\"{sandbox}\");");
    }
    code.AppendLine("}");

    File.WriteAllText(outputPath, code.ToString());
    Console.WriteLine($"Generated {hosts.Count} hosts -> {outputPath}");
}

// NSwag declares the serializer and init hooks per client class; each one forwards to WildberriesClientSetup
static void WriteHooks(IEnumerable<string> groups, string outputPath)
{
    var code = new StringBuilder();
    code.AppendLine("// <auto-generated>Generated by generator/generate-client.cs — do not edit by hand.</auto-generated>");
    code.AppendLine($"namespace {Namespace};");
    foreach (var group in groups)
    {
        code.AppendLine();
        code.AppendLine($"public partial class Wildberries{group}Client");
        code.AppendLine("{");
        code.AppendLine("    static partial void UpdateJsonSerializerSettings(System.Text.Json.JsonSerializerOptions settings) =>");
        code.AppendLine("        WildberriesClientSetup.ConfigureSerializer(settings);");
        code.AppendLine();
        code.AppendLine("    partial void Initialize() => ReadResponseAsString = WildberriesClientSetup.ReadResponseAsString;");
        code.AppendLine("}");
    }

    File.WriteAllText(outputPath, code.ToString());
}
