namespace ProjectWarehouse.Ops.Infrastructure;

/// Reads and rewrites a compose .env in place. Rewriting rather than regenerating is the point:
/// the same file holds POSTGRES_PASSWORD and Jwt__SecretKey.
public sealed class EnvFile
{
    private readonly List<string> _lines;

    private EnvFile(List<string> lines, string? newline)
    {
        _lines = lines;
        Newline = newline ?? "\n";
    }

    public string Newline { get; }

    public static EnvFile Parse(string content)
    {
        var newline = content.Contains("\r\n") ? "\r\n" : "\n";
        var lines = content.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        // A trailing newline leaves an empty final element; keeping it would grow the file on every write.
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return new EnvFile(lines, newline);
    }

    public string? Get(string key)
    {
        foreach (var line in _lines)
        {
            if (TrySplit(line, out var name, out var value) && name == key)
                return value;
        }

        return null;
    }

    public IReadOnlyDictionary<string, string> GetAll(IEnumerable<string> keys)
    {
        var wanted = new HashSet<string>(keys, StringComparer.Ordinal);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in _lines)
        {
            if (TrySplit(line, out var name, out var value) && wanted.Contains(name))
                found[name] = value;
        }

        return found;
    }

    /// Writes the last occurrence, because that is the one compose reads. Duplicates should have
    /// been rejected before it comes to this; the rule matters anyway so read and write agree.
    public void Set(string key, string value)
    {
        var last = LastIndexOf(key);

        if (last >= 0)
            _lines[last] = $"{key}={value}";
        else
            _lines.Add($"{key}={value}");
    }

    public void Remove(string key) =>
        _lines.RemoveAll(line => TrySplit(line, out var name, out _) && name == key);

    /// Keys defined more than once. Compose takes the last, an editor sees the first, and a
    /// rewrite of either is a deploy that reports success while nothing changed.
    public IReadOnlyList<string> Duplicates(IEnumerable<string> keys)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in _lines)
        {
            if (TrySplit(line, out var name, out _))
                counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        return [.. keys.Distinct(StringComparer.Ordinal).Where(key => counts.GetValueOrDefault(key) > 1)];
    }

    private int LastIndexOf(string key)
    {
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            if (TrySplit(_lines[i], out var name, out _) && name == key)
                return i;
        }

        return -1;
    }

    public string Render() => string.Join(Newline, _lines) + Newline;

    private static bool TrySplit(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            return false;

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
            return false;

        key = trimmed[..separator].Trim();
        value = trimmed[(separator + 1)..].Trim();
        return key.Length > 0;
    }
}
