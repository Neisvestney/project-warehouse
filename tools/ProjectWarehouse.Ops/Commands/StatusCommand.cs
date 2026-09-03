using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Registry;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class StatusCommand : AsyncCommand<TargetSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, TargetSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        var picked = TargetPicker.Pick(loaded.Config, settings.Target);
        if (picked is not { } target)
            return 1;

        using var registries = new RegistryClientFactory();
        if (target.Value.PullsFrom is { } registryName
            && loaded.Config.Registries.TryGetValue(registryName, out var registry))
        {
            registries.Prepare(registry);
        }

        var service = new StatusService(loaded.Config, registries);

        TargetStatus status;
        try
        {
            await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
            status = await AnsiConsole.Status()
                .StartAsync(
                    $"Reading {target.Key}…",
                    _ => service.ReadAsync(connection, cancellationToken));
        }
        catch (CommandHostException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        Render(target.Key, target.Value.Danger, status);
        return 0;
    }

    private static void Render(string name, bool danger, TargetStatus status)
    {
        var title = danger ? $"[red]{name}[/]" : $"[green]{name}[/]";
        AnsiConsole.MarkupLine($"\n{title}  [grey]{status.HostDescription}[/]");

        if (status.Git is { } git)
        {
            var dirty = git.Dirty ? "[yellow] (dirty)[/]" : string.Empty;
            AnsiConsole.MarkupLineInterpolated($"[grey]git[/]  {git.Commit}  {git.Subject}");
            if (git.Dirty)
                AnsiConsole.MarkupLine($"[grey]    [/]{dirty.Trim()}");
        }

        if (status.EnvFileError is { } envError)
            AnsiConsole.MarkupLineInterpolated($"[yellow]env  {envError}[/]");

        if (status.ExpectedRegistryValue is { } expected)
        {
            var actual = status.RegistryValue;
            var matches = string.Equals(actual, expected, StringComparison.Ordinal);
            var rendered = actual is null
                ? "[red]not set[/]"
                : matches ? $"[grey]{actual}[/]" : $"[yellow]{actual} (config says {expected})[/]";

            AnsiConsole.MarkupLine($"[grey]registry[/]  {rendered}");
        }

        var versions = new Table().Border(TableBorder.Rounded).Title("[grey]versions[/]");
        versions.AddColumns("service", "variable", "deployed", "registry");
        foreach (var service in status.Services)
        {
            versions.AddRow(
                service.Name,
                service.TagVariable,
                DescribeDeployed(service, status.HasEnvFile),
                DescribeLatest(service));
        }

        AnsiConsole.Write(versions);

        var containers = new Table().Border(TableBorder.Rounded).Title("[grey]containers[/]");
        containers.AddColumns("service", "state", "health", "image");
        foreach (var container in status.Containers)
        {
            containers.AddRow(
                container.Name,
                Colorize(container.State),
                container.Health is { Length: > 0 } health ? Colorize(health) : "[grey]—[/]",
                container.Image);
        }

        if (status.Containers.Count == 0)
            AnsiConsole.MarkupLine("[yellow]No containers for this compose project.[/]");
        else
            AnsiConsole.Write(containers);
    }

    private static string DescribeDeployed(ServiceStatus service, bool hasEnvFile)
    {
        if (service.DeployedTag is { } tag)
            return tag;

        return hasEnvFile ? "[red]not set[/]" : "[grey]—[/]";
    }

    private static string DescribeLatest(ServiceStatus service)
    {
        if (service.RegistryError is { } error)
            return $"[red]{Markup.Escape(error)}[/]";

        if (service.LatestTag is not { } latest)
            return "[grey]—[/]";

        var behind = service.DeployedTag is { } deployed
            && ImageVersion.TryParse(deployed, out var current)
            && ImageVersion.TryParse(latest, out var newest)
            && newest.CompareTo(current) > 0;

        return behind ? $"[yellow]{latest} (newer)[/]" : $"[grey]{latest}[/]";
    }

    private static string Colorize(string state) => state.ToLowerInvariant() switch
    {
        "running" or "healthy" => $"[green]{state}[/]",
        "starting" or "restarting" or "created" => $"[yellow]{state}[/]",
        "exited" or "dead" or "unhealthy" => $"[red]{state}[/]",
        _ => state,
    };
}
