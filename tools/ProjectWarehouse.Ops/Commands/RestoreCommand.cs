using System.ComponentModel;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class RestoreSettings : TargetSettings
{
    [CommandOption("--from <PATH>")]
    [Description("Backup directory to restore. Prompted from the local backups when omitted.")]
    public string? From { get; init; }

    [CommandOption("--parts <PARTS>")]
    [Description("Comma-separated parts to restore. Defaults to everything in the backup.")]
    public string? Parts { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation. A danger target still asks for its name.")]
    public bool Yes { get; init; }

    [CommandOption("--no-safety-backup")]
    [Description("Skip the backup taken before overwriting anything.")]
    public bool NoSafetyBackup { get; init; }
}

public sealed class RestoreCommand : AsyncCommand<RestoreSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, RestoreSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        var picked = TargetPicker.Pick(loaded.Config, settings.Target);
        if (picked is not { } target)
            return 1;

        var backupsRoot = loaded.Config.Local!.BackupsDir;
        var directory = SelectBackup(backupsRoot, settings.From);
        if (directory is null)
            return 1;

        BackupManifest manifest;
        try
        {
            manifest = await BackupManifest.ReadAsync(directory, cancellationToken);
        }
        catch (BackupException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var available = manifest.Parts.Select(part => part.Name).ToList();
        var parts = BackupParts.Select(available, settings.Parts);
        if (parts is null)
            return 1;

        if (!CheckKeysTogether(parts, available))
            return 1;

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);

        RenderPlan(target.Key, target.Value.Danger, manifest, directory, parts);

        if (!settings.NoSafetyBackup)
        {
            AnsiConsole.MarkupLine("\n[grey]Taking a backup of the current state first.[/]");
            var backup = new BackupService(connection);
            var safety = await BackupCommand.RunAsync(
                backup, backupsRoot, backup.AvailableParts(), connection, cancellationToken);

            if (safety is null)
            {
                AnsiConsole.MarkupLine("[red]The safety backup failed, so nothing was restored.[/]");
                return 1;
            }
        }

        var what = $"Restore {string.Join(", ", parts)} into {target.Key} — current data is replaced.";

        // --yes never covers a danger target: the whole point of typing the name is that the
        // answer cannot come from a script or from muscle memory.
        if (target.Value.Danger)
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]{target.Key} is a danger target and can only be restored from a terminal.[/]");
                return 1;
            }

            if (!DangerPrompt.Confirm(target.Key, what))
                return 0;
        }
        else if (!settings.Yes && !AnsiConsole.Confirm($"{what} Continue?", defaultValue: false))
        {
            return 0;
        }

        var appServices = target.Value.Services
            .Select(name => loaded.Config.Services[name].ComposeService)
            .ToList();

        return await RunAsync(
            new RestoreService(connection), directory, manifest, parts, appServices, cancellationToken);
    }

    private static string? SelectBackup(string backupsRoot, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var path = Path.GetFullPath(requested);
            if (Directory.Exists(path))
                return path;

            AnsiConsole.MarkupLineInterpolated($"[red]No such backup directory: {path}[/]");
            return null;
        }

        if (!Directory.Exists(backupsRoot))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]No backups in {backupsRoot}.[/]");
            return null;
        }

        var candidates = Directory
            .EnumerateDirectories(backupsRoot)
            .Where(directory => File.Exists(Path.Combine(directory, BackupManifest.FileName)))
            .OrderByDescending(directory => directory, StringComparer.Ordinal)
            .Take(20)
            .ToList();

        if (candidates.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]No backups in {backupsRoot}.[/]");
            return null;
        }

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine("[red]No terminal to prompt on. Pass --from <path>.[/]");
            return null;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Backup")
                .AddChoices(candidates)
                .UseConverter(Path.GetFileName));
    }

    /// The data protection key ring decrypts the marketplace API keys stored in the database.
    /// Restoring one without the other leaves every stored key undecryptable.
    private static bool CheckKeysTogether(IReadOnlyList<string> parts, IReadOnlyList<string> available)
    {
        var hasKeys = parts.Contains("keys", StringComparer.OrdinalIgnoreCase);
        var hasDatabase = parts.Contains(BackupManifest.DatabasePart, StringComparer.OrdinalIgnoreCase);

        if (hasKeys == hasDatabase)
            return true;

        if (!available.Contains("keys", StringComparer.OrdinalIgnoreCase))
            return true;

        AnsiConsole.MarkupLine(
            "[red]'db' and 'keys' have to be restored together: the key ring decrypts the marketplace "
                + "API keys held in the database, and either one alone makes them unreadable.[/]");

        return false;
    }

    private static void RenderPlan(
        string name,
        bool danger,
        BackupManifest manifest,
        string directory,
        IReadOnlyList<string> parts)
    {
        var title = danger ? $"[red]{name}[/]" : $"[green]{name}[/]";
        AnsiConsole.MarkupLine($"\n{title}  [grey]restore[/]");
        AnsiConsole.MarkupLineInterpolated($"[grey]from[/]  {directory}");
        AnsiConsole.MarkupLineInterpolated(
            $"[grey]taken[/] {manifest.TakenAt:yyyy-MM-dd HH:mm:ss} [grey]from[/] {manifest.Target}");

        var table = new Table().Border(TableBorder.Rounded).Title("[grey]parts[/]");
        table.AddColumns("part", "file", "size");

        foreach (var part in manifest.Parts)
        {
            var selected = parts.Contains(part.Name, StringComparer.OrdinalIgnoreCase);
            table.AddRow(
                selected ? $"[green]{part.Name}[/]" : $"[grey]{part.Name} (skipped)[/]",
                $"[grey]{Markup.Escape(part.File)}[/]",
                $"[grey]{part.Bytes / 1024.0 / 1024.0:F1} MB[/]");
        }

        AnsiConsole.Write(table);
    }

    private static async Task<int> RunAsync(
        RestoreService service,
        string directory,
        BackupManifest manifest,
        IReadOnlyList<string> parts,
        IReadOnlyList<string> appServices,
        CancellationToken cancellationToken)
    {
        RestoreOutcome outcome;
        try
        {
            outcome = await AnsiConsole.Status().StartAsync("starting", ctx =>
                service.ExecuteAsync(
                    directory,
                    manifest,
                    parts,
                    appServices,
                    step => ctx.Status(Markup.Escape(step)),
                    null,
                    cancellationToken));
        }
        catch (Exception ex) when (ex is BackupException or CommandHostException)
        {
            // Only the checks that run before the target is touched throw; everything past them
            // comes back as an outcome so the stack can be brought up on the way out.
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        foreach (var warning in outcome.Warnings)
            AnsiConsole.MarkupLineInterpolated($"[yellow]{warning}[/]");

        if (!outcome.Succeeded)
        {
            AnsiConsole.MarkupLineInterpolated($"\n[red]{outcome.Failure}[/]");
            AnsiConsole.MarkupLine("[yellow]Check `pwops status` before walking away.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("\n[green]restored[/]");
        return 0;
    }
}
