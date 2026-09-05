using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

/// Answers are printed back because the prompt does not leave them on screen, and a run started
/// from the menu would otherwise scroll past with nothing saying what it was asked to do. Values
/// that came from an option are echoed too — the log reads the same either way.
public static class Chosen
{
    /// <param name="value">Markup, so a name can keep the colour the list gave it.</param>
    public static void Show(string label, string value) =>
        AnsiConsole.MarkupLine($"[grey]{label,-9}[/]{value}");

    public static void ShowText(string label, string value) => Show(label, Markup.Escape(value));
}
