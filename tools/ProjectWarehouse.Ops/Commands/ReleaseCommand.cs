using System.ComponentModel;
using ProjectWarehouse.Ops.Configuration;
using ProjectWarehouse.Ops.Infrastructure.Docker;
using ProjectWarehouse.Ops.Registry;
using ProjectWarehouse.Ops.Services;
using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class ReleaseSettings : OpsSettings
{
    [CommandOption("-r|--registry <NAME>")]
    [Description("Registry to push to. Prompted when the config defines more than one.")]
    public string? Registry { get; init; }

    [CommandOption("-s|--service <NAME>")]
    [Description("Service to release; repeatable. Prompted when omitted.")]
    public string[] Services { get; init; } = [];

    [CommandOption("-b|--bump <PART>")]
    [Description("patch, minor or major. Prompted when omitted.")]
    public string? Bump { get; init; }

    [CommandOption("--version <VERSION>")]
    [Description("Exact version for every selected service, instead of an increment.")]
    public string? Version { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip the final confirmation.")]
    public bool Yes { get; init; }
}

public sealed class ReleaseCommand : AsyncCommand<ReleaseSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ReleaseSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        if (!CheckNonInteractive(loaded.Config, settings))
            return 1;

        var picked = RegistryPicker.Pick(loaded.Config, settings.Registry);
        if (picked is not { } registry)
            return 1;

        var selected = SelectServices(loaded.Config, settings);
        if (selected is null)
            return 1;

        if (settings.Version is { } exact && !ImageVersion.TryParse(exact, out _))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]'{exact}' is not a major.minor.patch version.[/]");
            return 1;
        }

        var bump = ParseBump(settings.Bump);
        if (settings.Bump is not null && bump is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown bump '{settings.Bump}'. Use patch, minor or major.[/]");
            return 1;
        }

        using var registries = new RegistryClientFactory();
        registries.Prepare(registry.Value);

        var docker = new DockerCli(loaded.ProjectDir);
        if (!await docker.IsAvailableAsync(cancellationToken))
        {
            AnsiConsole.MarkupLine("[red]docker is not reachable. Is the daemon running?[/]");
            return 1;
        }

        var service = new ReleaseService(registry.Value, registries.Create(registry.Value), docker);

        AnsiConsole.MarkupLineInterpolated(
            $"\n[grey]registry[/] {registry.Key} [grey]{registry.Value.ImagePrefix}[/]");

        var candidates = await AnsiConsole.Status()
            .StartAsync("Reading published tags…", _ => service.SurveyAsync(selected, cancellationToken));

        var items = BuildPlan(candidates, settings, bump);
        if (items.Count == 0)
            return 1;

        RenderPlan(registry.Value, candidates, items);

        if (!settings.Yes && !AnsiConsole.Confirm("Build and push?"))
            return 0;

        return await RunAsync(service, items, cancellationToken);
    }

    /// Without a terminal every prompt dies on the same unhelpful read error, so the missing
    /// options are named up front instead.
    private static bool CheckNonInteractive(OpsConfig config, ReleaseSettings settings)
    {
        if (AnsiConsole.Profile.Capabilities.Interactive)
            return true;

        var missing = new List<string>();

        if (settings.Registry is null && config.Registries.Count > 1)
            missing.Add("--registry");

        if (settings.Services.Length == 0 && config.Services.Count > 1)
            missing.Add("--service");

        if (settings.Bump is null && settings.Version is null)
            missing.Add("--bump or --version");

        if (!settings.Yes)
            missing.Add("--yes");

        if (missing.Count == 0)
            return true;

        AnsiConsole.MarkupLineInterpolated(
            $"[red]No terminal to prompt on. Pass {string.Join(", ", missing)}.[/]");

        return false;
    }

    private static List<KeyValuePair<string, ServiceConfig>>? SelectServices(
        OpsConfig config, ReleaseSettings settings)
    {
        if (config.Services.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No services defined.[/]");
            return null;
        }

        if (settings.Services.Length > 0)
        {
            var chosen = new List<KeyValuePair<string, ServiceConfig>>();
            foreach (var name in settings.Services)
            {
                if (!config.Services.TryGetValue(name, out var service))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Unknown service '{name}'.[/]");
                    return null;
                }

                chosen.Add(new KeyValuePair<string, ServiceConfig>(name, service));
            }

            return chosen;
        }

        if (config.Services.Count == 1)
            return [config.Services.First()];

        var names = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Services to release")
                .NotRequired()
                .AddChoices(config.Services.Keys)
                .Select(config.Services.Keys.First()));

        return names.Count == 0
            ? null
            : names.Select(name => new KeyValuePair<string, ServiceConfig>(name, config.Services[name]))
                .ToList();
    }

    internal static List<ReleaseItem> BuildPlan(
        IReadOnlyList<ReleaseCandidate> candidates, ReleaseSettings settings, VersionBump? bump)
    {
        var items = new List<ReleaseItem>();

        foreach (var candidate in candidates)
        {
            if (candidate.RegistryError is { } error)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]{candidate.ServiceName}: {error}[/]");
                return [];
            }

            var version = ResolveVersion(candidate, settings, bump);
            if (version is null)
                return [];

            items.Add(new ReleaseItem(candidate.ServiceName, candidate.Service, version.Value));
        }

        return items;
    }

    private static ImageVersion? ResolveVersion(
        ReleaseCandidate candidate, ReleaseSettings settings, VersionBump? bump)
    {
        if (settings.Version is { } exact)
            return ImageVersion.TryParse(exact, out var parsed) ? parsed : null;

        if (bump is { } requested)
            return ReleaseService.Next(candidate, requested);

        if (candidate.Current is null)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[grey]{candidate.ServiceName}: nothing published yet, starting at 0.0.1[/]");
            return ReleaseService.Next(candidate, VersionBump.Patch);
        }

        var choices = new[] { VersionBump.Patch, VersionBump.Minor, VersionBump.Major };
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<object>()
                .Title($"[bold]{candidate.ServiceName}[/]  [grey]{candidate.Current}[/] →")
                .AddChoices([.. choices.Cast<object>(), "exact version"])
                .UseConverter(item => item is VersionBump part
                    ? $"{ReleaseService.Next(candidate, part)}  [grey]{part.ToString().ToLowerInvariant()}[/]"
                    : "[grey]exact version…[/]"));

        if (choice is VersionBump picked)
            return ReleaseService.Next(candidate, picked);

        var typed = AnsiConsole.Prompt(
            new TextPrompt<string>("  version:")
                .Validate(value => ImageVersion.TryParse(value, out _)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]expected major.minor.patch[/]")));

        ImageVersion.TryParse(typed, out var exactVersion);
        return exactVersion;
    }

    internal static void RenderPlan(
        RegistryConfig registry, IReadOnlyList<ReleaseCandidate> candidates, IReadOnlyList<ReleaseItem> items)
    {
        var current = candidates.ToDictionary(candidate => candidate.ServiceName);
        var table = new Table().Border(TableBorder.Rounded).Title("[grey]plan[/]");
        table.AddColumns("service", "current", "next", "reference");

        foreach (var item in items)
        {
            table.AddRow(
                item.ServiceName,
                current[item.ServiceName].Current?.ToString() ?? "[grey]—[/]",
                $"[green]{item.Version}[/]",
                $"[grey]{Markup.Escape(item.Reference(registry))}[/]");
        }

        AnsiConsole.Write(table);
    }

    private static Task BuildAsync(
        ReleaseService service, ReleaseItem item, CancellationToken cancellationToken)
    {
        var pane = new LogPane(30);
        var header = $"{item.ServiceName} · build";

        return AnsiConsole.Live(pane.Render()).StartAsync(async ctx =>
        {
            // Called from the process reader threads; the pane locks, so no marshalling is needed.
            var work = service.BuildAsync(item, line => pane.Write(header, line), cancellationToken);

            while (!work.IsCompleted)
            {
                ctx.UpdateTarget(pane.Render());
                ctx.Refresh();
                await Task.WhenAny(work, Task.Delay(100, cancellationToken));
            }

            ctx.UpdateTarget(pane.Render());
            ctx.Refresh();
            await work;
        });
    }

    internal static async Task<int> RunAsync(
        ReleaseService service, IReadOnlyList<ReleaseItem> items, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var item in items)
            {
                await BuildAsync(service, item, cancellationToken);

                // Outside the live display on purpose: docker draws the push itself, and two
                // things redrawing the same screen produce neither picture.
                AnsiConsole.MarkupLineInterpolated($"\n[grey]{item.ServiceName} · push[/]");
                await service.PushAsync(item, cancellationToken);
            }
        }
        catch (ReleaseException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            foreach (var line in LastLines(ex.Log, 30))
                AnsiConsole.WriteLine(line);

            return 1;
        }

        AnsiConsole.WriteLine();
        foreach (var item in items)
            AnsiConsole.MarkupLineInterpolated($"[green]pushed[/] {item.ServiceName} {item.Version}");

        return 0;
    }

    private static IEnumerable<string> LastLines(string log, int count) =>
        log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(count)
            .Select(line => line.TrimEnd('\r'));

    internal static VersionBump? ParseBump(string? value) => value?.ToLowerInvariant() switch
    {
        "patch" => VersionBump.Patch,
        "minor" => VersionBump.Minor,
        "major" => VersionBump.Major,
        _ => null,
    };
}
