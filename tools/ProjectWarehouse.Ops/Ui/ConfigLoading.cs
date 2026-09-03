using ProjectWarehouse.Ops.Commands;
using ProjectWarehouse.Ops.Configuration;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class ConfigLoading
{
    /// Loads and validates, printing the failure itself. Returns null when the config is unusable.
    public static LoadedConfig? LoadOrReport(OpsSettings settings)
    {
        LoadedConfig loaded;
        try
        {
            loaded = OpsConfigLoader.Load(settings.ConfigPath, settings.ProjectDir);
        }
        catch (OpsConfigException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Config error:[/] {ex.Message}");
            return null;
        }

        var errors = OpsConfigValidator.Validate(loaded.Config);
        if (errors.Count == 0)
            return loaded;

        AnsiConsole.MarkupLine($"[red]Config is invalid[/] ({errors.Count} problem(s)):");
        foreach (var error in errors)
            AnsiConsole.MarkupLineInterpolated($"  [red]•[/] {error}");

        return null;
    }

    public static void PrintSource(LoadedConfig loaded)
    {
        var chain = string.Join(" → ", loaded.SourceChain.Reverse());
        AnsiConsole.MarkupLineInterpolated($"[grey]config [/] {chain}");
        AnsiConsole.MarkupLineInterpolated($"[grey]project[/] {loaded.ProjectDir}");
    }
}
