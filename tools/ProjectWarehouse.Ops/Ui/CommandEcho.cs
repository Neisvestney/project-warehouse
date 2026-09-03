using ProjectWarehouse.Ops.Commands;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

/// Prints the command line that repeats what the menu just did. Off for a typed command — there
/// the user already has the line, and echoing it back is noise.
public static class CommandEcho
{
    public static bool Enabled { get; set; }

    /// <param name="parts">Null or empty entries are dropped, so options can be built inline.</param>
    public static void Suggest(OpsSettings settings, params string?[] parts)
    {
        if (!Enabled)
            return;

        var args = new List<string>();

        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
                args.Add(part);
        }

        if (settings.ConfigPath is { } configPath)
            args.AddRange(["--config", configPath]);

        if (settings.ProjectDir is { } projectDir)
            args.AddRange(["--project", projectDir]);

        AnsiConsole.MarkupLineInterpolated(
            $"[grey]repeat with:[/] [grey]pwops {string.Join(' ', args.Select(Quote))}[/]");
    }

    private static string Quote(string value) =>
        value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
