using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class ValidateCommand : Command<OpsSettings>
{
    protected override int Execute(CommandContext context, OpsSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        ConfigLoading.PrintSource(loaded);
        AnsiConsole.MarkupLine("[green]Config is valid.[/]");
        AnsiConsole.WriteLine();

        var config = loaded.Config;

        var paths = new Table().Border(TableBorder.Rounded).Title("[grey]local paths[/]");
        paths.AddColumns("setting", "resolved");
        paths.AddRow("backupsDir", config.Local!.BackupsDir);
        paths.AddRow("telemetryArchiveDir", config.Local.TelemetryArchiveDir);
        AnsiConsole.Write(paths);

        var registries = new Table().Border(TableBorder.Rounded).Title("[grey]registries[/]");
        registries.AddColumns("name", "image prefix", "api", "credentials");
        foreach (var (name, registry) in config.Registries)
        {
            registries.AddRow(
                name,
                registry.ImagePrefix,
                registry.Api.ToString().ToLowerInvariant(),
                registry.Credentials.ToString().ToLowerInvariant());
        }

        var services = new Table().Border(TableBorder.Rounded).Title("[grey]services[/]");
        services.AddColumns("name", "image", "compose service", "tag variable");
        foreach (var (name, service) in config.Services)
            services.AddRow(name, service.Image, service.ComposeService, service.TagVariable);

        var targets = new Table().Border(TableBorder.Rounded).Title("[grey]targets[/]");
        targets.AddColumns("name", "kind", "pulls from", "services", "compose file");
        foreach (var (name, target) in config.Targets)
        {
            targets.AddRow(
                TargetPicker.Decorate(name, target),
                target.Kind.ToString().ToLowerInvariant(),
                target.PullsFrom ?? "[grey]—[/]",
                string.Join(", ", target.Services),
                target.ComposeFile);
        }

        if (config.Registries.Count > 0)
            AnsiConsole.Write(registries);
        if (config.Services.Count > 0)
            AnsiConsole.Write(services);
        AnsiConsole.Write(targets);

        return 0;
    }
}
