using System.ComponentModel;
using Spectre.Console.Cli;

namespace ProjectWarehouse.Ops.Commands;

public class OpsSettings : CommandSettings
{
    [CommandOption("-c|--config <PATH>")]
    [Description("Path to ops.json. Defaults to the working directory, then the executable directory.")]
    public string? ConfigPath { get; init; }

    [CommandOption("-p|--project <PATH>")]
    [Description("Path to the code repository. Defaults to the nearest .git ancestor of the working directory.")]
    public string? ProjectDir { get; init; }
}

public class TargetSettings : OpsSettings
{
    [CommandArgument(0, "[target]")]
    [Description("Target name from the config. Prompted when omitted.")]
    public string? Target { get; init; }
}
