using ProjectWarehouse.Ops.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public sealed class MenuCommand : AsyncCommand<OpsSettings>
{
    /// Set by the composition root. The menu dispatches by re-entering the same command app, so a
    /// menu entry and a typed command run exactly the same code down to argument parsing.
    internal static Func<string[], Task<int>>? Runner { get; set; }

    private static readonly (string Command, string Title, string Hint)[] Entries =
    [
        ("release", "Release", "build images and push them to the registry"),
        ("deploy", "Deploy", "roll versions out to a target"),
        ("ship", "Ship", "release, then deploy what was just built"),
        ("status", "Status", "what is deployed and running on a target"),
        ("backup", "Backup", "download the database, keys and data files"),
        ("restore", "Restore", "restore a target from a local backup"),
        ("telemetry", "Telemetry", "download the telemetry archive"),
        ("validate", "Validate", "check the config chain"),
        ("exit", "Exit", string.Empty),
    ];

    protected override async Task<int> ExecuteAsync(
        CommandContext context, OpsSettings settings, CancellationToken cancellationToken)
    {
        var loaded = ConfigLoading.LoadOrReport(settings);
        if (loaded is null)
            return 1;

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine("[red]The menu needs a terminal. Run `pwops --help` for the commands.[/]");
            return 1;
        }

        AnsiConsole.Write(new FigletText("pw ops").Color(Color.Teal));
        ConfigLoading.PrintSource(loaded);

        var targets = loaded.Config.Targets.Select(
            pair => TargetPicker.Describe(pair.Key, pair.Value));

        Chosen.Show("targets", string.Join("  ", targets));

        var globals = new List<string>();
        if (settings.ConfigPath is { } configPath)
            globals.AddRange(["--config", configPath]);

        if (settings.ProjectDir is { } projectDir)
            globals.AddRange(["--project", projectDir]);

        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<(string Command, string Title, string Hint)>()
                    .Title("What now?")
                    .PageSize(Entries.Length + 2)
                    .AddChoices(Entries)
                    .AddCancelResult(Entries[^1])
                    .UseConverter(entry => entry.Hint.Length == 0
                        ? entry.Title
                        : $"{entry.Title,-10} [grey]{entry.Hint}[/]"));

            if (choice.Command == "exit")
                return 0;

            // Names the run and separates it from the one above: a session is one long scroll,
            // and two commands' output otherwise runs together.
            AnsiConsole.Write(
                new Rule($"[grey]{choice.Command}[/]").RuleStyle(Style.Parse("grey")));

            if (Runner is not { } runner)
                return 1;

            try
            {
                // Only a menu-driven run echoes its command line; a typed one already has it.
                CommandEcho.Enabled = true;
                await runner([choice.Command, .. globals]);
            }
            catch (OperationCanceledException)
            {
                // One cancelled command does not end the session; Exit does.
                AnsiConsole.MarkupLine("[yellow]cancelled[/]");
            }
            finally
            {
                CommandEcho.Enabled = false;
            }
        }

        return 0;
    }
}
