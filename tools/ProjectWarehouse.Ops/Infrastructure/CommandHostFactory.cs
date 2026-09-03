using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Infrastructure;

public static class CommandHostFactory
{
    public static ICommandHost Create(TargetConfig target, string localWorkingDirectory) =>
        target.Kind switch
        {
            TargetKind.Local => new LocalCommandHost(localWorkingDirectory),
            TargetKind.Ssh => SshCommandHost.Connect(
                target.Ssh ?? throw new CommandHostException("Target has kind 'ssh' but no ssh section.")),
            _ => throw new CommandHostException($"Unsupported target kind '{target.Kind}'."),
        };
}
