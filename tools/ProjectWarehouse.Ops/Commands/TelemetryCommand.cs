using System.ComponentModel;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class TelemetrySettings : TargetSettings
{
    [CommandOption("--since <DAYS>")]
    [Description("Only files modified within this many days. Defaults to the whole archive.")]
    public int? Since { get; init; }

    [CommandOption("--to <PATH>")]
    [Description("Where to extract. Defaults to local.telemetryArchiveDir.")]
    public string? To { get; init; }

    [CommandOption("--clean")]
    [Description("Empty the local archive first, so the replay stack shows this fetch alone.")]
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

        var destination = settings.To is { } to
            ? Path.GetFullPath(to)
            : loaded.Config.Local!.TelemetryArchiveDir;

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
        var service = new TelemetryService(connection);

        TelemetryDownload download;
        try
        {
            download = await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new DownloadedColumn(), new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("starting", maxValue: double.MaxValue);
                    var progress = new Progress<long>(bytes => task.Value = bytes);

                    return await service.DownloadAsync(
                        destination,
                        settings.Since,
                        settings.Clean,
                        step => task.Description = Markup.Escape(step),
                        progress,
                        cancellationToken);
                });
        }
        catch (Exception ex) when (ex is BackupException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        if (download.Files == 0)
        {
            AnsiConsole.MarkupLine(
                settings.Since is { } days
                    ? $"[yellow]Nothing written in the last {days} day(s).[/]"
                    : "[yellow]The archive is empty.[/]");

            return 0;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"\n[green]{download.Files} file(s)[/], {download.Bytes / 1024.0 / 1024.0:F1} MB → {download.Directory}");

        AnsiConsole.MarkupLine(
            "[grey]docker compose -f docker-compose.telemetry.yml up -d[/] "
                + "[grey]→ http://localhost:18890[/]");

        return 0;
    }
}
