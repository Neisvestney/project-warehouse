using System.ComponentModel;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class TelemetrySettings : TargetSettings
{
    [CommandOption("--signals <SIGNALS>")]
    [Description("Comma-separated signals to fetch, e.g. traces,metrics,logs. Prompted when omitted.")]
    public string? Signals { get; init; }

    [CommandOption("--rotated")]
    [Description("Also fetch the rotation backups, not just what each signal is being written to.")]
    public bool Rotated { get; init; }

    [CommandOption("--since <DAYS>")]
    [Description("Only rotation backups modified within this many days. Defaults to all of them.")]
    public int? Since { get; init; }

    [CommandOption("--to <PATH>")]
    [Description("Where to extract. Defaults to local.telemetryArchiveDir.")]
    public string? To { get; init; }

    [CommandOption("--clean")]
    [Description("Drop the chosen signals from the local archive first, rotations included, so the replay stack shows this fetch alone.")]
    public bool Clean { get; init; }
}

public sealed class TelemetryCommand : AsyncCommand<TelemetrySettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, TelemetrySettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        var picked = TargetPicker.Pick(loaded.Config, settings.Target);
        if (picked is not { } target)
            return 1;

        if (settings.Since is <= 0)
        {
            AnsiConsole.MarkupLine("[red]--since must be greater than zero.[/]");
            return 1;
        }

        var signals = TelemetrySignalPicker.Pick(settings.Signals);
        if (signals is null)
            return 1;

        var scope = TelemetryScopePicker.Pick(settings.Rotated, settings.Since);

        if (scope is { Rotated: false, SinceDays: not null })
        {
            AnsiConsole.MarkupLine(
                "[yellow]--since only narrows the rotation backups; add --rotated to fetch them.[/]");
        }

        var destination = settings.To is { } to
            ? Path.GetFullPath(to)
            : loaded.Config.Local!.TelemetryArchiveDir;

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
        var service = new TelemetryService(connection);
        var request = new TelemetryRequest(
            signals, scope.Rotated, scope.SinceDays, settings.Clean);

        TelemetryDownload download;
        try
        {
            download = await ProgressReporter.RunAsync(
                reporter => service.DownloadAsync(
                    destination, request, reporter, cancellationToken));
        }
        catch (Exception ex) when (ex is BackupException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        foreach (var warning in download.Warnings)
            AnsiConsole.MarkupLineInterpolated($"[yellow]{warning}[/]");

        if (download.Files == 0)
        {
            // Only blame the window where it actually applied: a `--since` that never reached the
            // rotation backups would otherwise take the blame for signals that hold nothing.
            if (scope is { Rotated: true, SinceDays: { } days })
                AnsiConsole.MarkupLineInterpolated(
                    $"[yellow]Nothing written in the last {days} day(s).[/]");
            else
                AnsiConsole.MarkupLineInterpolated(
                    $"[yellow]The target holds nothing for {string.Join(", ", signals)}.[/]");

            return 0;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"\n[green]{download.Files} file(s)[/], {ByteSize.Format(download.Bytes)} → {download.Directory}");

        AnsiConsole.MarkupLineInterpolated(
            $"[grey]{ByteSize.Format(download.Transferred)} over the wire[/]");

        AnsiConsole.MarkupLine(
            "[grey]docker compose -f docker-compose.telemetry.yml up -d[/] "
                + "[grey]→ http://localhost:18890[/]");

        CommandEcho.Suggest(
            settings,
            "telemetry",
            target.Key,
            "--signals",
            string.Join(',', signals),
            scope.Rotated ? "--rotated" : null,
            scope.SinceDays is not null ? "--since" : null,
            scope.SinceDays?.ToString(),
            settings.To is not null ? "--to" : null,
            settings.To,
            settings.Clean ? "--clean" : null);

        return 0;
    }
}

internal sealed record TelemetryScope(bool Rotated, int? SinceDays);

/// The window is asked only once the rotation backups are in, because it narrows nothing else —
/// the order is what tells the user the two belong together.
internal static class TelemetryScopePicker
{
    private static readonly (string Label, int? Days)[] Windows =
    [
        ("everything", null),
        ("last day", 1),
        ("last 3 days", 3),
        ("last 7 days", 7),
        ("last 30 days", 30),
    ];

    public static TelemetryScope Pick(bool rotated, int? sinceDays)
    {
        // An option given is an answer already, and a run with no terminal to ask on takes the
        // default rather than waiting for a keypress.
        if (rotated
            || sinceDays is not null
            || !AnsiConsole.Profile.Capabilities.Interactive)
        {
            return Show(rotated, sinceDays);
        }

        if (!AnsiConsole.Confirm("Fetch rotation backups?", defaultValue: false))
            return Show(false, null);

        var window = AnsiConsole.Prompt(
            new SelectionPrompt<(string Label, int? Days)>()
                .Title("Window")
                .AddChoices(Windows)
                .UseConverter(choice => choice.Label));

        return Show(true, window.Days);
    }

    private static TelemetryScope Show(bool rotated, int? sinceDays)
    {
        var scope = rotated ? "active files + rotations" : "active files";

        Chosen.ShowText(
            "scope", sinceDays is { } days ? $"{scope}, last {days} day(s)" : scope);

        return new TelemetryScope(rotated, sinceDays);
    }
}

internal static class TelemetrySignalPicker
{
    public static IReadOnlyList<string>? Pick(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return Prompt();

        var chosen = new List<string>();

        foreach (var signal in requested.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Canonical spelling, not the user's: the name becomes a file-name glob on the target,
            // and `--signals Traces` would otherwise match nothing and read as an empty archive.
            var match = TelemetrySignals.All.FirstOrDefault(
                name => name.Equals(signal, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]Unknown signal '{signal}'. Known: {string.Join(", ", TelemetrySignals.All)}.[/]");

                return null;
            }

            if (!chosen.Contains(match, StringComparer.Ordinal))
                chosen.Add(match);
        }

        // A string of nothing but separators parses to no signals at all without ever entering the
        // loop above — `--signals "$SIGNALS"` with the variable unset is the way that happens.
        if (chosen.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]--signals named none. Known: {string.Join(", ", TelemetrySignals.All)}.[/]");

            return null;
        }

        Chosen.ShowText("signals", string.Join(", ", chosen));
        return chosen;
    }

    private static IReadOnlyList<string> Prompt()
    {
        // Everything, unchanged, wherever there is no terminal to ask on — a scripted run must not
        // start waiting for a keypress.
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            Chosen.ShowText("signals", string.Join(", ", TelemetrySignals.All));
            return TelemetrySignals.All;
        }

        // Required, so clearing every box cannot produce a fetch that transfers nothing.
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Signals")
            .InstructionsText("[grey]space toggles, enter confirms[/]")
            .Required()
            .AddChoices(TelemetrySignals.All);

        foreach (var signal in TelemetrySignals.All)
            prompt.Select(signal);

        var selected = AnsiConsole.Prompt(prompt);

        Chosen.ShowText("signals", string.Join(", ", selected));
        return selected;
    }
}
