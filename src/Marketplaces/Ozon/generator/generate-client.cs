#!/usr/bin/env dotnet
#:project ../../Generator/Neisvestney.Marketplaces.Generator/Neisvestney.Marketplaces.Generator.csproj

// Trims the Ozon Seller API spec down to the paths we actually use and generates a C# client.
//
//   dotnet run src/Marketplaces/Ozon/generator/generate-client.cs -- --fetch   fetch, trim, generate
//   dotnet run src/Marketplaces/Ozon/generator/generate-client.cs              trim and generate from the raw file
//
// The `--` separator is required, otherwise dotnet swallows --fetch as its own argument.

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Neisvestney.Marketplaces.Generator;

const string SpecUrl = "https://docs.ozon.ru/api/seller/swagger.json";
const string BaseUrl = "https://api-seller.ozon.ru";

var here = ScriptDirectory();
var rawPath = Path.Combine(here, "ozon-swagger.raw.json");
var trimmedPath = Path.Combine(here, "ozon-openapi.trimmed.json");
var whitelistPath = Path.Combine(here, "paths.whitelist.json");
var outputPath = Path.GetFullPath(Path.Combine(here, "..",
    "Neisvestney.Marketplaces.Ozon", "Generated", "OzonApiClient.g.cs"));

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
await ClientGenerator.GenerateAsync(trimmedPath,
    ClientGenerator.DefaultSettings("Neisvestney.Marketplaces.Ozon", "OzonApiClient", "OzonApiException"),
    outputPath,
    enumConverter: "global::Neisvestney.Marketplaces.Shared.TolerantEnumConverter");
return 0;

static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

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
    var root = SpecLoader.LoadJson(rawPath);
    var whitelist = SpecLoader.LoadJson(whitelistPath);
    var include = whitelist["include"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
    var binaryResponses = whitelist["binaryResponses"]?.AsArray().Select(n => n!.GetValue<string>()).ToHashSet() ?? [];

    var paths = SpecTrimmer.SelectPaths(root, include);
    SpecTrimmer.StripParameters(paths, name => name is "Client-Id" or "Api-Key");

    foreach (var (path, _, operation) in SpecTrimmer.Operations(paths).Where(o => binaryResponses.Contains(o.Path)))
        SpecSanitizer.NormalizeBinaryResponse(path, operation);

    var trimmed = SpecTrimmer.Trim(root, paths);
    var document = new JsonObject
    {
        ["openapi"] = root["openapi"]!.DeepClone(),
        ["info"] = root["info"]!.DeepClone(),
        // the spec ships "//api-seller.ozon.ru" (no scheme), which NSwag parses incorrectly
        ["servers"] = new JsonArray(new JsonObject { ["url"] = BaseUrl }),
        ["paths"] = trimmed.Paths,
        ["components"] = trimmed.Components,
    };

    // Ozon terms forbid copying the documentation
    SpecSanitizer.StripDocumentation(document);
    SpecSanitizer.Sanitize(document);
    SpecSanitizer.FlattenDottedSchemaNames(document);

    SpecLoader.Save(document, trimmedPath);
    Console.WriteLine($"Trimmed {root["paths"]!.AsObject().Count} paths -> {paths.Count}, " +
                      $"{new FileInfo(rawPath).Length / 1024} KB -> {new FileInfo(trimmedPath).Length / 1024} KB");
}
