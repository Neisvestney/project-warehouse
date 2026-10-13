using System.Text.Json.Nodes;

namespace Neisvestney.Marketplaces.Generator;

/// <summary>The kept operations of one source spec plus every component they reach.</summary>
public sealed record TrimmedSpec(JsonObject Paths, JsonObject Components);

public static class SpecTrimmer
{
    private static readonly HashSet<string> Methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    /// <summary>
    /// Copies the whitelisted entries out of <c>paths</c>. An entry is either a bare path, keeping every
    /// operation under it, or <c>"POST /path"</c>, keeping one — read-only clients must not pull in the
    /// PUT/DELETE that share a path with a read.
    /// </summary>
    public static JsonObject SelectPaths(JsonObject root, IEnumerable<string> entries)
    {
        var allPaths = root["paths"]!.AsObject();
        var kept = new JsonObject();
        foreach (var entry in entries)
        {
            var separator = entry.IndexOf(' ');
            var method = separator < 0 ? null : entry[..separator].ToLowerInvariant();
            var path = separator < 0 ? entry : entry[(separator + 1)..];

            if (!allPaths.TryGetPropertyValue(path, out var item) || item is not JsonObject source)
                throw new InvalidOperationException($"Whitelisted path missing from the spec: {path}");

            if (method is null)
            {
                kept[path] = source.DeepClone();
                continue;
            }

            if (source[method] is not JsonObject operation)
                throw new InvalidOperationException($"Whitelisted operation missing from the spec: {entry}");

            if (kept[path] is not JsonObject target)
            {
                target = new JsonObject();
                foreach (var (key, value) in source.Where(p => !Methods.Contains(p.Key)))
                    target[key] = value?.DeepClone();
                kept[path] = target;
            }
            target[method] = operation.DeepClone();
        }
        return kept;
    }

    public static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonObject paths) =>
        from path in paths
        from operation in path.Value!.AsObject()
        where Methods.Contains(operation.Key) && operation.Value is JsonObject
        select (path.Key, operation.Key, (JsonObject)operation.Value!);

    /// <summary>
    /// Removes header/query parameters the auth handler supplies, so they never reach a method signature.
    /// Matches both inline parameters and <c>$ref</c>s by their component name.
    /// </summary>
    public static void StripParameters(JsonObject paths, Func<string, bool> isStripped)
    {
        foreach (var owner in paths.Select(p => p.Value).OfType<JsonObject>()
                     .SelectMany(item => item.Select(o => o.Value).OfType<JsonObject>().Prepend(item)))
        {
            if (owner["parameters"] is not JsonArray parameters)
                continue;

            var survivors = parameters
                .Where(p => !(p is JsonObject o && (o["$ref"]?.GetValue<string>()?.Split('/')[^1] ?? o["name"]?.GetValue<string>()) is { } name && isStripped(name)))
                .Select(p => p!.DeepClone())
                .ToArray();

            if (survivors.Length == 0)
                owner.Remove("parameters");
            else
                owner["parameters"] = new JsonArray(survivors);
        }
    }

    /// <summary>Keeps only the components reachable from <paramref name="paths"/>, names sorted per section.</summary>
    public static TrimmedSpec Trim(JsonObject root, JsonObject paths)
    {
        var reachable = CollectReachableComponents(paths, root);

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

        return new TrimmedSpec(paths, components);
    }

    private static HashSet<(string Section, string Name)> CollectReachableComponents(JsonNode from, JsonObject root)
    {
        var reachable = new HashSet<(string, string)>();
        var queue = new Queue<JsonNode>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            foreach (var reference in JsonTree.FindReferences(queue.Dequeue()))
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

    /// <summary>
    /// Merges trimmed specs from several source files into one. A component name that two sources define
    /// differently is renamed to <c>{prefix}{name}</c> in each of them — the WB sections each carry their own
    /// <c>Error</c> / <c>Response4XX</c>. Identical definitions collapse into one. Renames mutate the input specs.
    /// </summary>
    public static TrimmedSpec Merge(IReadOnlyList<(string Prefix, TrimmedSpec Spec)> parts)
    {
        var collisions = parts
            .SelectMany(p => p.Spec.Components.SelectMany(s => s.Value!.AsObject().Select(c => (Section: s.Key, Name: c.Key, Body: c.Value!))))
            .GroupBy(c => (c.Section, c.Name))
            .Where(g => g.Skip(1).Any(c => !JsonNode.DeepEquals(c.Body, g.First().Body)))
            .Select(g => g.Key)
            .ToHashSet();

        var taken = parts
            .SelectMany(p => p.Spec.Components.SelectMany(s => s.Value!.AsObject().Select(c => (s.Key, c.Key))))
            .Where(k => !collisions.Contains(k))
            .ToHashSet();

        var paths = new JsonObject();
        var components = new JsonObject();
        foreach (var (prefix, spec) in parts)
        {
            foreach (var (section, name) in collisions.Where(c => spec.Components[c.Section]?[c.Name] is not null).OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                var target = prefix + name;
                for (var i = 2; !taken.Add((section, target)); i++)
                    target = prefix + name + i;
                RenameComponent(spec, section, name, target);
                Console.WriteLine($"  collision: {section}/{name} -> {target}");
            }

            foreach (var (section, items) in spec.Components)
            {
                var merged = components[section] as JsonObject ?? (JsonObject)(components[section] = new JsonObject());
                foreach (var (name, body) in items!.AsObject())
                {
                    // equal before the renames, different after: they reference components that collided
                    if (merged[name] is { } kept && !JsonNode.DeepEquals(kept, body))
                        throw new InvalidOperationException($"{section}/{name} from {prefix} diverged after collision renames; merge it by hand");
                    merged[name] ??= body!.DeepClone();
                }
            }

            foreach (var (path, item) in spec.Paths)
            {
                if (paths[path] is not JsonObject existing)
                {
                    paths[path] = item!.DeepClone();
                    continue;
                }
                foreach (var (key, value) in item!.AsObject().Where(o => !Methods.Contains(o.Key)))
                    if (!JsonNode.DeepEquals(existing[key], value))
                        throw new InvalidOperationException($"{path} declares a different path-level {key} in two sources");
                foreach (var (method, operation) in item.AsObject().Where(o => Methods.Contains(o.Key)))
                {
                    if (existing.ContainsKey(method))
                        throw new InvalidOperationException($"{method.ToUpperInvariant()} {path} is whitelisted in two sources");
                    existing[method] = operation!.DeepClone();
                }
            }
        }

        foreach (var section in components.Select(s => s.Value!.AsObject()))
        {
            var sorted = section.OrderBy(c => c.Key, StringComparer.Ordinal).ToArray();
            section.Clear();
            foreach (var (name, body) in sorted)
                section[name] = body;
        }

        return new TrimmedSpec(paths, components);
    }

    private static void RenameComponent(TrimmedSpec spec, string section, string from, string to)
    {
        var items = spec.Components[section]!.AsObject();
        var body = items[from]!;
        items.Remove(from);
        items[to] = body;

        var renames = new Dictionary<string, string> { [$"#/components/{section}/{from}"] = $"#/components/{section}/{to}" };
        JsonTree.RewriteReferences(spec.Paths, renames);
        JsonTree.RewriteReferences(spec.Components, renames);
    }
}
