using System.ComponentModel;
using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure;
using ProjectWarehouse.Ops.Registry;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class DeploySettings : TargetSettings
{
    [CommandOption("--set <SERVICE=VERSION>")]
    [Description("Version to deploy for one service; repeatable. Prompted when omitted.")]
    public string[] Set { get; init; } = [];

    [CommandOption("--health-timeout <SECONDS>")]
    [Description("How long to wait for containers to become healthy. Default 180.")]
    public int HealthTimeout { get; init; } = 180;

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation.")]
    public bool Yes { get; init; }
}

public sealed class DeployCommand : AsyncCommand<DeploySettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, DeploySettings settings, CancellationToken cancellationToken)
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
                $"[red]targets.{target.Key} has no pullsFrom, so there is nothing to deploy.[/]");
            return 1;
        }

        if (settings.HealthTimeout <= 0)
        {
            AnsiConsole.MarkupLine("[red]--health-timeout must be greater than zero.[/]");
            return 1;
        }

        var registry = loaded.Config.Registries[registryName];
        var requested = ParseSet(settings.Set, target.Value.Services);
        if (requested is null)
            return 1;

        using var registries = new RegistryClientFactory();
        registries.Prepare(registry);

        await using var connection = TargetContext.Open(target.Key, target.Value, loaded.ProjectDir);
        var service = new DeploymentService(connection);

        var services = target.Value.Services
            .Select(name => (Name: name, Config: loaded.Config.Services[name]))
            .ToList();

        var variables = services.Select(entry => entry.Config.TagVariable).ToList();
        if (target.Value.RegistryVariable is { } registryVariable)
            variables.Add(registryVariable);

        DeployPreflight preflight;
        try
        {
            preflight = await AnsiConsole.Status().StartAsync(
                $"Reading {target.Key}…",
                _ => service.PreflightAsync(registry, variables, cancellationToken));
        }
        catch (Exception ex) when (ex is DeploymentException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var selections = await ResolveVersionsAsync(
            services, requested, preflight, registry, registries, settings, cancellationToken);

        if (selections is null)
            return 1;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var selection in selections)
            values[selection.Service.TagVariable] = selection.Version;

        if (target.Value.RegistryVariable is { } variable)
            values[variable] = registry.ImagePrefix;

        RenderPlan(target.Key, target.Value.Danger, connection, preflight, values);

        if (!settings.Yes && !Confirm(target.Value))
            return 0;

        return await RunAsync(
            service,
            preflight,
            values,
            [.. services.Select(entry => entry.Config.ComposeService)],
            TimeSpan.FromSeconds(settings.HealthTimeout),
            cancellationToken);
    }

    private static Dictionary<string, string>? ParseSet(string[] pairs, IReadOnlyList<string> services)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in pairs)
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]--set expects service=version, got '{pair}'.[/]");
                return null;
            }

            var version = pair[(separator + 1)..];
            if (!ImageVersion.TryParse(version, out _))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]'{version}' is not a major.minor.patch version.[/]");
                return null;
            }

            var name = pair[..separator];

            // A typo here would otherwise be dropped in silence and the service prompted for as
            // though nothing had been passed.
            if (!services.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]'{name}' is not a service of this target. Known: {string.Join(", ", services)}.[/]");
                return null;
            }

            values[name] = version;
        }

        return values;
    }

    private static async Task<List<DeploySelection>?> ResolveVersionsAsync(
        List<(string Name, ServiceConfig Config)> services,
        Dictionary<string, string> requested,
        DeployPreflight preflight,
        RegistryConfig registry,
        RegistryClientFactory registries,
        DeploySettings settings,
        CancellationToken cancellationToken)
    {
        var selections = new List<DeploySelection>();
        var client = registries.Create(registry);

        foreach (var (name, config) in services)
        {
            if (requested.TryGetValue(name, out var version))
            {
                selections.Add(new DeploySelection(name, config, version));
                continue;
            }

            if (!AnsiConsole.Profile.Capabilities.Interactive)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]No terminal to prompt on. Pass --set {name}=<version>.[/]");
                return null;
            }

            IReadOnlyList<string> tags;
            try
            {
                tags = await AnsiConsole.Status().StartAsync(
                    $"Reading tags for {name}…", _ => client.ListTagsAsync(config.Image, cancellationToken));
            }
            catch (Exception ex) when (ex is RegistryException or HttpRequestException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{name}: {ex.Message}[/]");
                return null;
            }

            var published = tags
                .Where(tag => ImageVersion.TryParse(tag, out _))
                .Select(tag => ImageVersion.TryParse(tag, out var parsed) ? parsed : default)
                .Distinct()
                .OrderByDescending(parsed => parsed)
                .Take(10)
                .Select(parsed => parsed.ToString())
                .ToList();

            if (published.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{name}: nothing published in {registry.Host}.[/]");
                return null;
            }

            var deployed = preflight.Snapshot.GetValueOrDefault(config.TagVariable);
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]{name}[/]  [grey]deployed: {deployed ?? "—"}[/]")
                    .AddChoices(published)
                    .UseConverter(tag => tag == deployed ? $"{tag} [grey](current)[/]" : tag));

            selections.Add(new DeploySelection(name, config, choice));
        }

        return selections;
    }

    private static void RenderPlan(
        string name,
        bool danger,
        TargetContext connection,
        DeployPreflight preflight,
        IReadOnlyDictionary<string, string> values)
    {
        var title = danger ? $"[red]{name}[/]" : $"[green]{name}[/]";
        AnsiConsole.MarkupLine($"\n{title}  [grey]{connection.Host.Description}[/]");

        if (preflight.Git is { } git)
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]git[/]  {git.Commit}  {git.Subject}");
            if (git.StatusError is { } statusError)
                AnsiConsole.MarkupLineInterpolated($"[red]     cannot read working tree: {statusError}[/]");
            else if (git.Dirty)
                AnsiConsole.MarkupLine("[yellow]     working tree has uncommitted changes[/]");
        }

        var table = new Table().Border(TableBorder.Rounded)
            .Title($"[grey]{Markup.Escape(connection.Config.EnvFile ?? "env")}[/]");
        table.AddColumns("variable", "current", "next");

        foreach (var (key, next) in values)
        {
            var current = preflight.Snapshot.GetValueOrDefault(key);
            var changed = !string.Equals(current, next, StringComparison.Ordinal);

            table.AddRow(
                key,
                current is null ? "[red]not set[/]" : $"[grey]{Markup.Escape(current)}[/]",
                changed ? $"[green]{Markup.Escape(next)}[/]" : "[grey]unchanged[/]");
        }

        AnsiConsole.Write(table);
    }

    private static bool Confirm(TargetConfig target) =>
        target.Danger
            ? AnsiConsole.Confirm("[red]This is a danger target.[/] Deploy?", defaultValue: false)
            : AnsiConsole.Confirm("Deploy?");

    private static async Task<int> RunAsync(
        DeploymentService service,
        DeployPreflight preflight,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<string> composeServices,
        TimeSpan healthTimeout,
        CancellationToken cancellationToken)
    {
        DeployOutcome outcome;
        try
        {
            outcome = await AnsiConsole.Status().StartAsync("Starting…", ctx =>
                service.ExecuteAsync(
                    preflight,
                    values,
                    composeServices,
                    healthTimeout,
                    step => ctx.Status(Markup.Escape(step)),
                    cancellationToken));
        }
        catch (Exception ex) when (ex is DeploymentException or CommandHostException)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        if (outcome.Succeeded)
        {
            AnsiConsole.MarkupLine("\n[green]deployed[/]");
            return 0;
        }

        AnsiConsole.MarkupLineInterpolated($"\n[red]{outcome.Failure}[/]");

        if (outcome.RolledBack)
            AnsiConsole.MarkupLine("[yellow]Rolled back to the previous versions.[/]");
        else if (outcome.RollbackFailure is { } rollbackFailure)
            AnsiConsole.MarkupLineInterpolated($"[red]Rollback incomplete — {rollbackFailure}[/]");

        if (outcome.Logs is { Length: > 0 } logs)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(logs.TrimEnd());
        }

        return 1;
    }
}
