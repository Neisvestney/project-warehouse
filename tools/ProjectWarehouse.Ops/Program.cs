using ProjectWarehouse.Ops.Commands;
using ProjectWarehouse.Ops.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

// A fresh app per invocation: the menu dispatches by running the same parser again, and nothing
// here depends on a CommandApp being safe to re-enter while one of its commands is still on the
// stack.
static CommandApp<MenuCommand> CreateApp()
{
    var app = new CommandApp<MenuCommand>();

    app.Configure(config =>
    {
        config.SetApplicationName("pwops");

        config.AddCommand<ValidateCommand>("validate")
            .WithDescription("Load the config chain and report every problem.");

        config.AddCommand<ReleaseCommand>("release")
            .WithDescription("Build images and push them to the registry.");

        config.AddCommand<DeployCommand>("deploy")
            .WithDescription("Roll versions out to a target.");

        config.AddCommand<BackupCommand>("backup")
            .WithDescription("Download the database and volumes of a target.");

        config.AddCommand<RestoreCommand>("restore")
            .WithDescription("Restore a target from a local backup.");

        config.AddCommand<TelemetryCommand>("telemetry")
            .WithDescription("Download the telemetry archive for local replay.");

        config.AddCommand<StatusCommand>("status")
            .WithDescription("Show what is deployed and running on a target.");
    });

    return app;
}

MenuCommand.Runner = commandArgs => CreateApp().RunAsync(commandArgs);

// Left unset without a terminal, so a scripted run fails with a message instead of blocking on a
// prompt nobody can answer.
if (AnsiConsole.Profile.Capabilities.Interactive)
{
    SshCommandHost.PassphrasePrompt = (keyPath, attempt) =>
    {
        if (attempt == 1)
            AnsiConsole.MarkupLineInterpolated($"[grey]{keyPath} is encrypted.[/]");
        else
            AnsiConsole.MarkupLine("[yellow]Wrong passphrase.[/]");

        return AnsiConsole.Prompt(
            new TextPrompt<string>("  passphrase:").Secret().AllowEmpty());
    };
}

return CreateApp().Run(args);
