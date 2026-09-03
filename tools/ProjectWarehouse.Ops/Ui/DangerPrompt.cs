using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class DangerPrompt
{
    /// Typing the target's name, not pressing enter: the whole point is that the answer cannot be
    /// given by muscle memory.
    public static bool Confirm(string targetName, string what)
    {
        AnsiConsole.MarkupLineInterpolated($"\n[red]⚠  {what}[/]");

        var typed = AnsiConsole.Prompt(
            new TextPrompt<string>($"Type [red]{targetName}[/] to confirm, anything else to abort:")
                .AllowEmpty());

        var confirmed = string.Equals(typed.Trim(), targetName, StringComparison.Ordinal);
        if (!confirmed)
            AnsiConsole.MarkupLine("[grey]Aborted.[/]");

        return confirmed;
    }
}
