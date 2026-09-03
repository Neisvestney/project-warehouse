using System.ComponentModel;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Infrastructure.Docker;
using ProjectWarehouse.Ops.Registry;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class ShipSettings : TargetSettings
{
    [CommandOption("-b|--bump <PART>")]
    [Description("patch, minor or major. Prompted when omitted.")]
    public string? Bump { get; init; }

    [CommandOption("--version <VERSION>")]
    [Description("Exact version for every service, instead of an increment.")]
    public string? Version { get; init; }

    [CommandOption("--health-timeout <SECONDS>")]
    [Description("How long to wait for containers to become healthy. Default 180.")]
    public int HealthTimeout { get; init; } = 180;

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation.")]
    public bool Yes { get; init; }
}

/// Release and deploy in one pass. The services are the target's, the registry is the one it
/// pulls from, and the versions deployed are exactly the ones just built — nothing is chosen twice.
public sealed class ShipCommand : AsyncCommand<ShipSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ShipSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        var picked = TargetPicker.Pick(loaded.Config, settings.Target);
        if (picked is not { } target)
            return 1;

        if (target.Value.PullsFrom is not { } registryName)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]targets.{target.Key} has no pullsFrom, so there is nothing to ship to it.[/]");
            return 1;
        }

        if (settings.HealthTimeout <= 0)
        {
            AnsiConsole.MarkupLine("[red]--health-timeout must be greater than zero.[/]");
            return 1;
        }

        if (settings.Version is { } exact && !ImageVersion.TryParse(exact, out _))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]'{exact}' is not a major.minor.patch version.[/]");
            return 1;
        }

        var bump = ReleaseCommand.ParseBump(settings.Bump);
        if (settings.Bump is not null && bump is null)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]Unknown bump '{settings.Bump}'. Use patch, minor or major.[/]");
            return 1;
        }

        if (!AnsiConsole.Profile.Capabilities.Interactive
            && (settings is { Bump: null, Version: null } || !settings.Yes))
        {
            AnsiConsole.MarkupLine("[red]No terminal to prompt on. Pass --bump or --version, and --yes.[/]");
            return 1;
        }

        var registry = loaded.Config.Registries[registryName];
        var services = target.Value.Services
            .Select(name => new KeyValuePair<string, Configuration.ServiceConfig>(
                name, loaded.Config.Services[name]))
            .ToList();

        using var registries = new RegistryClientFactory();
        registries.Prepare(registry);

        var docker = new DockerCli(loaded.ProjectDir);
        if (!await docker.IsAvailableAsync(cancellationToken))
        {
            AnsiConsole.MarkupLine("[red]docker is not reachable. Is the daemon running?[/]");
            return 1;
        }

        var release = new ReleaseService(registry, registries.Create(registry), docker);

        AnsiConsole.MarkupLineInterpolated(
            $"\n[grey]registry[/] {registryName} [grey]{registry.ImagePrefix}[/]");

        var candidates = await AnsiConsole.Status()
            .StartAsync("Reading published tags…", _ => release.SurveyAsync(services, cancellationToken));

        var items = ReleaseCommand.BuildPlan(
            candidates,
            new ReleaseSettings { Bump = settings.Bump, Version = settings.Version, Yes = settings.Yes },
            bump);

        if (items.Count == 0)
            return 1;

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
        var deployment = new DeploymentService(connection);

        var variables = services.Select(entry => entry.Value.TagVariable).ToList();
        if (target.Value.RegistryVariable is { } registryVariable)
            variables.Add(registryVariable);

        // The target is inspected before the build, not after: a dirty working tree or a duplicated
        // variable is worth finding out about now rather than four minutes into docker build.
        DeployPreflight preflight;
        try
        {
            preflight = await AnsiConsole.Status().StartAsync(
                $"Reading {target.Key}…",
                _ => deployment.PreflightAsync(registry, variables, cancellationToken));
        }
        catch (Exception ex) when (ex is DeploymentException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items)
            values[item.Service.TagVariable] = item.Version.ToString();

        if (target.Value.RegistryVariable is { } variable)
            values[variable] = registry.ImagePrefix;

        ReleaseCommand.RenderPlan(registry, candidates, items);
        DeployCommand.RenderPlan(target.Key, target.Value.Danger, connection, preflight, values);

        if (!settings.Yes && !Confirm(target.Value))
            return 0;

        var released = await ReleaseCommand.RunAsync(release, items, cancellationToken);
        if (released != 0)
            return released;

        var exitCode = await DeployCommand.RunAsync(
            deployment,
            preflight,
            values,
            [.. services.Select(entry => entry.Value.ComposeService)],
            TimeSpan.FromSeconds(settings.HealthTimeout),
            cancellationToken);

        // Ship carries one --version for every service, so a run that ended on mixed versions has
        // no single line that would repeat it.
        var versions = items.Select(item => item.Version.ToString()).Distinct().ToList();
        if (exitCode == 0 && versions.Count == 1)
            CommandEcho.Suggest(settings, "ship", target.Key, "--version", versions[0], "--yes");

        return exitCode;
    }

    private static bool Confirm(Configuration.TargetConfig target) =>
        target.Danger
            ? AnsiConsole.Confirm("[red]This is a danger target.[/] Build, push and deploy?", defaultValue: false)
            : AnsiConsole.Confirm("Build, push and deploy?");
}
