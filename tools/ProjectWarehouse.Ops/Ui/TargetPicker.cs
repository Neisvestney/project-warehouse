using ProjectWarehouse.Ops.Configuration;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class TargetPicker
{
    /// Danger is the only risk marker the tool knows about — it drives every colored name.
    public static string Decorate(string name, TargetConfig target) =>
        target.Danger ? $"[red]{name}[/]" : $"[green]{name}[/]";

    /// The name as the list shows it, so the echo and the choice read alike.
    public static string Describe(string name, TargetConfig target) =>
        $"{Decorate(name, target)} [grey]({target.Kind.ToString().ToLowerInvariant()})[/]";

    public static KeyValuePair<string, TargetConfig>? Pick(OpsConfig config, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (config.Targets.TryGetValue(requested, out var target))
            {
                Chosen.Show("target", Describe(requested, target));
                return new KeyValuePair<string, TargetConfig>(requested, target);
            }

            AnsiConsole.MarkupLineInterpolated($"[red]Unknown target '{requested}'.[/]");
            return null;
        }

        var names = config.Targets.Keys.ToList();
        if (names.Count == 0)
            return null;

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Target")
                .AddChoices(names)
                .UseConverter(name => Describe(name, config.Targets[name])));

        Chosen.Show("target", Describe(picked, config.Targets[picked]));
        return new KeyValuePair<string, TargetConfig>(picked, config.Targets[picked]);
    }
}
