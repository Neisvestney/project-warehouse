using ProjectWarehouse.Ops.Configuration;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class RegistryPicker
{
    public static KeyValuePair<string, RegistryConfig>? Pick(OpsConfig config, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (config.Registries.TryGetValue(requested, out var registry))
                return new KeyValuePair<string, RegistryConfig>(requested, registry);

            AnsiConsole.MarkupLineInterpolated($"[red]Unknown registry '{requested}'.[/]");
            return null;
        }

        if (config.Registries.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No registries defined.[/]");
            return null;
        }

        if (config.Registries.Count == 1)
            return config.Registries.First();

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Registry")
                .AddChoices(config.Registries.Keys)
                .UseConverter(name => $"{name} [grey]{config.Registries[name].ImagePrefix}[/]"));

        return new KeyValuePair<string, RegistryConfig>(picked, config.Registries[picked]);
    }
}
