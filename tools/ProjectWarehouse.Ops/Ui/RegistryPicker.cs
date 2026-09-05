using ProjectWarehouse.Ops.Configuration;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class RegistryPicker
{
    public static string Describe(string name, RegistryConfig registry) =>
        $"{name} [grey]{registry.ImagePrefix}[/]";

    public static KeyValuePair<string, RegistryConfig>? Pick(OpsConfig config, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (config.Registries.TryGetValue(requested, out var registry))
            {
                Chosen.Show("registry", Describe(requested, registry));
                return new KeyValuePair<string, RegistryConfig>(requested, registry);
            }

            AnsiConsole.MarkupLineInterpolated($"[red]Unknown registry '{requested}'.[/]");
            return null;
        }

        if (config.Registries.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No registries defined.[/]");
            return null;
        }

        if (config.Registries.Count == 1)
        {
            var only = config.Registries.First();
            Chosen.Show("registry", Describe(only.Key, only.Value));
            return only;
        }

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Registry")
                .AddChoices(config.Registries.Keys)
                .UseConverter(name => Describe(name, config.Registries[name])));

        Chosen.Show("registry", Describe(picked, config.Registries[picked]));
        return new KeyValuePair<string, RegistryConfig>(picked, config.Registries[picked]);
    }
}
