using System.ComponentModel;
using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class BackupSettings : TargetSettings
{
    [CommandOption("--parts <PARTS>")]
    [Description("Comma-separated parts to back up, e.g. db,keys,datafiles. Defaults to all.")]
    public string? Parts { get; init; }
}

public sealed class BackupCommand : AsyncCommand<BackupSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, BackupSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        var picked = TargetPicker.Pick(loaded.Config, settings.Target);
        if (picked is not { } target)
            return 1;

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
        var service = new BackupService(connection);

        var parts = BackupParts.Select(service.AvailableParts(), settings.Parts);
        if (parts is null)
            return 1;

        var directory = await RunAsync(
            service, loaded.Config.Local!.BackupsDir, parts, connection, cancellationToken);

        return directory is null ? 1 : 0;
    }

    internal static async Task<string?> RunAsync(
        BackupService service,
        string backupsRoot,
        IReadOnlyList<string> parts,
        TargetContext connection,
        CancellationToken cancellationToken)
    {
        var versions = await ReadVersionsAsync(connection, cancellationToken);

        try
        {
            var directory = await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new DownloadedColumn(), new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("starting", maxValue: double.MaxValue);
                    var offset = 0L;
                    var current = 0L;

                    var progress = new Progress<long>(bytes =>
                    {
                        current = bytes;
                        task.Value = offset + bytes;
                    });

                    return await service.CreateAsync(
                        backupsRoot,
                        parts,
                        versions,
                        step =>
                        {
                            offset += current;
                            current = 0;
                            task.Description = Markup.Escape(step);
                        },
                        progress,
                        cancellationToken);
                });

            var total = Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);

            AnsiConsole.MarkupLineInterpolated(
                $"\n[green]saved[/] {directory} [grey]({ByteSize.Format(total)})[/]");

            return directory;
        }
        catch (Exception ex) when (ex is BackupException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return null;
        }
    }

    /// Recorded so a restore can tell which image the data belonged to.
    private static async Task<Dictionary<string, string>> ReadVersionsAsync(
        TargetContext connection, CancellationToken cancellationToken)
    {
        if (connection.EnvFilePath is not { } path)
            return [];

        var content = await connection.Host.ReadFileAsync(path, cancellationToken);
        if (content is null)
            return [];

        var keys = new List<string>();
        if (connection.Config.RegistryVariable is { } registryVariable)
            keys.Add(registryVariable);

        return EnvFile.Parse(content).GetAll(keys).ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

internal static class BackupParts
{
    public static IReadOnlyList<string>? Select(IReadOnlyList<string> available, string? requested)
    {
        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]This target defines neither postgres nor volumes.[/]");
            return null;
        }

        if (string.IsNullOrWhiteSpace(requested))
            return available;

        var chosen = new List<string>();

        foreach (var part in requested.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Canonical spelling, not the user's: everything downstream matches part names
            // ordinally, and `--parts DB` would otherwise pass here and fail mid-restore.
            var match = available.FirstOrDefault(
                name => name.Equals(part, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]Unknown part '{part}'. Known: {string.Join(", ", available)}.[/]");
                return null;
            }

            chosen.Add(match);
        }

        return chosen;
    }
}
