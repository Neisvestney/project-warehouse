using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectWarehouse.Ops.Infrastructure;

namespace ProjectWarehouse.Ops.Configuration;

public static class OpsConfigLoader
{
    public const string DefaultFileName = "ops.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // A typo in a field name must not silently disarm a flag — `danger` gates the destructive actions.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static LoadedConfig Load(string? explicitPath, string? projectDir)
    {
        var path = Resolve(explicitPath);
        var project = ResolveProjectDir(projectDir);
        var chain = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var config = LoadRecursive(path, project, visited, chain);
        config.Local ??= new LocalPathsConfig();
        return new LoadedConfig(config, chain, project);
    }

    private static string ResolveProjectDir(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            return RepoRoot.Find();

        var expanded = Path.GetFullPath(PathHelper.Expand(projectDir));
        if (!Directory.Exists(expanded))
            throw new OpsConfigException($"--project directory not found: {expanded}");

        return expanded;
    }

    public static string Resolve(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var expanded = Path.GetFullPath(PathHelper.Expand(explicitPath));
            if (!File.Exists(expanded))
                throw new OpsConfigException($"Config file not found: {expanded}");

            return expanded;
        }

        foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var candidate = Path.Combine(dir, DefaultFileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new OpsConfigException(
            $"No {DefaultFileName} found in the working directory ({Directory.GetCurrentDirectory()}) "
                + $"or next to the executable ({AppContext.BaseDirectory}). "
                + $"Pass --config <path>, or copy ops.example.json to {DefaultFileName} "
                + "and point includeConfig at your private config.");
    }

    private static OpsConfig LoadRecursive(
        string path, string projectDir, HashSet<string> visited, List<string> chain)
    {
        if (!visited.Add(path))
            throw new OpsConfigException($"includeConfig cycle detected at {path}");

        var current = Read(path);
        var configDir = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        ExpandPaths(current, configDir, projectDir);

        OpsConfig merged;
        if (string.IsNullOrWhiteSpace(current.IncludeConfig))
        {
            merged = new OpsConfig();
        }
        else
        {
            var includePath = ResolveInclude(current.IncludeConfig, path);
            merged = LoadRecursive(includePath, projectDir, visited, chain);
        }

        chain.Add(path);
        Overlay(merged, current);
        return merged;
    }

    private static OpsConfig Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<OpsConfig>(File.ReadAllText(path), JsonOptions)
                ?? throw new OpsConfigException($"Config file is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new OpsConfigException($"Cannot parse {path}: {ex.Message}");
        }
    }

    private static void ExpandPaths(OpsConfig config, string configDir, string projectDir)
    {
        string Expand(string value) => PathTemplate.Expand(value, configDir, projectDir);

        if (config.IncludeConfig is { } include)
            config.IncludeConfig = Expand(include);

        if (config.Local is { } local)
        {
            // Local paths are consumed by this machine's file APIs, so they get normalized and
            // rooted here; everything else may well be a POSIX path on the far side of an SSH link.
            local.BackupsDir = Path.GetFullPath(Expand(local.BackupsDir), projectDir);
            local.TelemetryArchiveDir = Path.GetFullPath(Expand(local.TelemetryArchiveDir), projectDir);
        }

        foreach (var service in config.Services.Values)
        {
            service.Dockerfile = Expand(service.Dockerfile);
            service.Context = Expand(service.Context);
        }

        foreach (var target in config.Targets.Values)
        {
            target.ComposeFile = Expand(target.ComposeFile);

            if (target.EnvFile is { } envFile)
                target.EnvFile = Expand(envFile);

            if (target.RepoDir is { } repoDir)
                target.RepoDir = Expand(repoDir);

            if (target.Ssh?.KeyPath is { } keyPath)
                target.Ssh.KeyPath = Expand(keyPath);
        }
    }

    private static string ResolveInclude(string include, string includingFile)
    {
        var expanded = PathHelper.Expand(include);
        var basedir = Path.GetDirectoryName(includingFile) ?? Directory.GetCurrentDirectory();
        var full = Path.GetFullPath(expanded, basedir);
        if (!File.Exists(full))
            throw new OpsConfigException($"includeConfig target not found: {full} (from {includingFile})");

        return full;
    }

    /// Entries are replaced whole, not merged field by field.
    private static void Overlay(OpsConfig target, OpsConfig source)
    {
        if (source.Local is { } local)
            target.Local = local;

        foreach (var (key, value) in source.Registries)
            target.Registries[key] = value;

        foreach (var (key, value) in source.Services)
            target.Services[key] = value;

        foreach (var (key, value) in source.Targets)
            target.Targets[key] = value;
    }
}
