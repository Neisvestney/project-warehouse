namespace ProjectWarehouse.Ops.Infrastructure;

/// Where a command runs — this machine or a target over SSH. Every scenario is written against
/// this, so backup and deploy read the same on a local compose stack and on prod.
public interface ICommandHost : IAsyncDisposable
{
    string Description { get; }

    Task<CommandResult> RunAsync(ShellCommand command, CancellationToken cancellationToken);

    /// Copies stdout straight into <paramref name="destination"/>; stderr is buffered and returned.
    Task<CommandResult> RunStreamingAsync(
        ShellCommand command,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken);

    Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken);

    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken);

    /// Writes beside the target and renames over it, so a failure mid-write cannot leave a
    /// half-written file behind — the .env being replaced also holds the database password.
    Task ReplaceFileAsync(string path, string content, CancellationToken cancellationToken);

    /// Restricts a file to its owner. The .env and its backup hold the database password and the
    /// JWT signing key, and a freshly created file gets whatever the umask says.
    Task ProtectFileAsync(string path, CancellationToken cancellationToken);

    /// Copies a local file onto the host. Restores need the archive on the far side before any
    /// container can read it.
    Task UploadFileAsync(
        string localPath, string remotePath, IProgress<long>? progress, CancellationToken cancellationToken);

    Task<string> CreateTempDirectoryAsync(CancellationToken cancellationToken);

    Task RemoveDirectoryAsync(string path, CancellationToken cancellationToken);
}

public sealed class CommandHostException(string message, Exception? inner = null)
    : Exception(message, inner);
