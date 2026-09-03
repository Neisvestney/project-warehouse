using System.Text;
using ProjectWarehouse.Ops.Configuration;
using Renci.SshNet;

namespace ProjectWarehouse.Ops.Infrastructure;

public sealed class SshCommandHost : ICommandHost
{
    private readonly SshConfig _config;
    private readonly SshClient _ssh;
    private readonly Lazy<SftpClient> _sftp;

    private SshCommandHost(SshConfig config, ConnectionInfo connection)
    {
        _config = config;
        _ssh = new SshClient(connection);
        // PublicationOnly so a transient connect failure is not cached: the default mode would
        // hand the same exception to every later call, including the rollback write.
        _sftp = new Lazy<SftpClient>(
            () =>
            {
                var client = new SftpClient(connection);
                client.Connect();
                return client;
            },
            LazyThreadSafetyMode.PublicationOnly);
    }

    public string Description => $"{_config.User}@{_config.Host}:{_config.Port}";

    public static SshCommandHost Connect(SshConfig config)
    {
        var host = new SshCommandHost(config, BuildConnection(config));
        try
        {
            host._ssh.Connect();
        }
        catch (Exception ex)
        {
            throw new CommandHostException($"SSH connection to {host.Description} failed: {ex.Message}", ex);
        }

        return host;
    }

    public async Task<CommandResult> RunAsync(ShellCommand command, CancellationToken cancellationToken)
    {
        using var ssh = _ssh.CreateCommand(command.ToPosixLine());
        await ssh.ExecuteAsync(cancellationToken);
        return new CommandResult(ssh.ExitStatus ?? -1, ssh.Result, ssh.Error);
    }

    public async Task<CommandResult> RunStreamingAsync(
        ShellCommand command,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using var ssh = _ssh.CreateCommand(command.ToPosixLine());
        var execution = ssh.BeginExecute();

        var faulted = false;
        try
        {
            await LocalCommandHost.CopyWithProgressAsync(
                ssh.OutputStream, destination, progress, cancellationToken);
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            // Branching on the copy's outcome rather than the token: cancellation requested just
            // as the copy completed would otherwise skip EndExecute, leave ExitStatus null, and
            // report a finished dump as a failure.
            if (faulted)
                ssh.CancelAsync();
            else
                ssh.EndExecute(execution);
        }

        return new CommandResult(ssh.ExitStatus ?? -1, string.Empty, ssh.Error);
    }

    public Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var sftp = _sftp.Value;
        return Task.FromResult(sftp.Exists(path) ? sftp.ReadAllText(path, Encoding.UTF8) : null);
    }

    public Task WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        _sftp.Value.WriteAllText(path, content, Encoding.UTF8);
        return Task.CompletedTask;
    }

    public async Task ReplaceFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".pwops.tmp";

        // The rename replaces the inode, so the original mode has to be carried over explicitly
        // or a 0600 .env comes back world-readable.
        var mode = await ReadModeAsync(path, cancellationToken);

        _sftp.Value.WriteAllText(temporary, content, Encoding.UTF8);

        var chmod = await RunAsync(
            ShellCommand.Of("chmod", mode ?? "600", temporary), cancellationToken);

        if (!chmod.Succeeded)
        {
            await RunAsync(ShellCommand.Of("rm", "-f", temporary), CancellationToken.None);
            throw new CommandHostException($"Could not set mode on {temporary}: {chmod.FailureMessage}");
        }

        var move = await RunAsync(ShellCommand.Of("mv", "-f", temporary, path), cancellationToken);
        if (!move.Succeeded)
        {
            await RunAsync(ShellCommand.Of("rm", "-f", temporary), CancellationToken.None);
            throw new CommandHostException($"Could not replace {path}: {move.FailureMessage}");
        }
    }

    public async Task ProtectFileAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunAsync(ShellCommand.Of("chmod", "600", path), cancellationToken);
        if (!result.Succeeded)
            throw new CommandHostException($"Could not restrict {path}: {result.FailureMessage}");
    }

    private async Task<string?> ReadModeAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunAsync(ShellCommand.Of("stat", "-c", "%a", path), cancellationToken);
        var mode = result.StdOut.Trim();

        return result.Succeeded && mode.Length is 3 or 4 && mode.All(char.IsAsciiDigit) ? mode : null;
    }

    public async Task UploadFileAsync(
        string localPath,
        string remotePath,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(localPath);
        var sftp = _sftp.Value;

        await Task.Run(
            () => sftp.UploadFile(source, remotePath, uploaded => progress?.Report((long)uploaded)),
            cancellationToken);
    }

    public async Task<string> CreateTempDirectoryAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            ShellCommand.Of("mktemp", "-d", "-t", "pwops-XXXXXXXX"), cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"Could not create a temp directory: {result.FailureMessage}");

        return result.StdOut.Trim();
    }

    public async Task RemoveDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        // Guarded because the argument is assembled from config and a failure here would delete
        // whatever the empty string expands to.
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            throw new CommandHostException($"Refusing to remove '{path}'.");

        var result = await RunAsync(ShellCommand.Of("rm", "-rf", path), cancellationToken);
        if (!result.Succeeded)
            throw new CommandHostException($"Could not remove {path}: {result.FailureMessage}");
    }

    public ValueTask DisposeAsync()
    {
        if (_sftp.IsValueCreated)
            _sftp.Value.Dispose();

        _ssh.Dispose();
        return ValueTask.CompletedTask;
    }

    private static ConnectionInfo BuildConnection(SshConfig config)
    {
        var keyPath = string.IsNullOrWhiteSpace(config.KeyPath)
            ? null
            : PathHelper.Expand(config.KeyPath);

        if (keyPath is null)
            throw new CommandHostException($"ssh.keyPath is not set for {config.User}@{config.Host}.");

        if (!File.Exists(keyPath))
            throw new CommandHostException($"SSH key not found: {keyPath}");

        PrivateKeyFile key;
        try
        {
            key = string.IsNullOrEmpty(config.Passphrase)
                ? new PrivateKeyFile(keyPath)
                : new PrivateKeyFile(keyPath, config.Passphrase);
        }
        catch (Exception ex)
        {
            throw new CommandHostException($"Cannot read SSH key {keyPath}: {ex.Message}", ex);
        }

        return new ConnectionInfo(
            config.Host,
            config.Port,
            config.User,
            new PrivateKeyAuthenticationMethod(config.User, key));
    }
}
