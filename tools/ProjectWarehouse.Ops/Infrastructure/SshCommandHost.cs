using System.Text;
using ProjectWarehouse.Ops.Configuration;
using Renci.SshNet;
using Renci.SshNet.Common;

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
        try
        {
            using var ssh = _ssh.CreateCommand(command.ToPosixLine());
            await ssh.ExecuteAsync(cancellationToken);
            return new CommandResult(ssh.ExitStatus ?? -1, ssh.Result, ssh.Error);
        }
        catch (Exception ex) when (Foreign(ex))
        {
            throw new CommandHostException(
                $"{command.Executable} failed on {Description}: {ex.Message}", ex);
        }
    }

    /// A dropped link, a timeout, a disposed session — SSH.NET's own exceptions, which callers of
    /// <see cref="ICommandHost"/> have no way to name. Connecting already reports failure this way;
    /// running is the half that did not, so a caller narrowing on the interface's own exception
    /// type missed everything that went wrong after the session was up.
    private static bool Foreign(Exception ex) =>
        ex is not (CommandHostException or OperationCanceledException);

    public async Task<CommandResult> RunStreamingAsync(
        ShellCommand command,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Awaited inside the try on purpose: returning the task unawaited would leave the
            // catch below covering only the synchronous run-up to the first await.
            return await StreamAsync(command, destination, progress, cancellationToken);
        }
        catch (Exception ex) when (Foreign(ex))
        {
            throw new CommandHostException(
                $"{command.Executable} failed on {Description}: {ex.Message}", ex);
        }
    }

    private async Task<CommandResult> StreamAsync(
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
        try
        {
            var sftp = _sftp.Value;
            return Task.FromResult(sftp.Exists(path) ? sftp.ReadAllText(path, Encoding.UTF8) : null);
        }
        catch (Exception ex) when (Foreign(ex))
        {
            throw new CommandHostException($"Could not read {path} on {Description}: {ex.Message}", ex);
        }
    }

    public Task WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        try
        {
            _sftp.Value.WriteAllText(path, content, Encoding.UTF8);
            return Task.CompletedTask;
        }
        catch (Exception ex) when (Foreign(ex))
        {
            throw new CommandHostException($"Could not write {path} on {Description}: {ex.Message}", ex);
        }
    }

    public async Task ReplaceFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".pwops.tmp";

        // The rename replaces the inode, so the original mode has to be carried over explicitly
        // or a 0600 .env comes back world-readable.
        var mode = await ReadModeAsync(path, cancellationToken);

        await WriteFileAsync(temporary, content, cancellationToken);

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
        // Opened outside the guard below: a local file that cannot be read is not the target's
        // fault, and naming the host in that message would point at the wrong machine.
        await using var source = File.OpenRead(localPath);

        try
        {
            var sftp = _sftp.Value;

            await Task.Run(
                () => sftp.UploadFile(source, remotePath, uploaded => progress?.Report((long)uploaded)),
                cancellationToken);
        }
        catch (Exception ex) when (Foreign(ex))
        {
            throw new CommandHostException(
                $"Could not upload {remotePath} to {Description}: {ex.Message}", ex);
        }
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

    /// Runs from the `await using` that wraps a whole command, so neither half may escape or skip
    /// the other: a session already dropped would otherwise turn a finished backup into a failure,
    /// or leave the ssh client undisposed because the sftp one threw on the way out.
    public ValueTask DisposeAsync()
    {
        if (_sftp.IsValueCreated)
            Quietly(_sftp.Value.Dispose);

        Quietly(_ssh.Dispose);
        return ValueTask.CompletedTask;
    }

    private static void Quietly(Action dispose)
    {
        try
        {
            dispose();
        }
        catch (Exception)
        {
            // Closing a connection that is already gone is not a problem anyone can act on, and
            // the caller is on its way out regardless.
        }
    }

    /// Asked for a passphrase the config does not carry. Takes the key path and the attempt
    /// number, returns the passphrase, or null to give up. Set by the composition root; a session
    /// without a terminal leaves it unset and gets an error instead of a hang.
    public static Func<string, int, string?>? PassphrasePrompt { get; set; }

    private const int PassphraseAttempts = 3;

    private static PrivateKeyFile LoadKey(string keyPath, string? passphrase)
    {
        try
        {
            if (!string.IsNullOrEmpty(passphrase))
                return new PrivateKeyFile(keyPath, passphrase);

            return new PrivateKeyFile(keyPath);
        }
        catch (SshPassPhraseNullOrEmptyException)
        {
            // Only reached when the key is encrypted and the config said nothing about it.
        }
        catch (Exception ex)
        {
            throw new CommandHostException($"Cannot read SSH key {keyPath}: {ex.Message}", ex);
        }

        if (PassphrasePrompt is not { } prompt)
        {
            throw new CommandHostException(
                $"{keyPath} is encrypted and no passphrase is configured. Add one under "
                    + "overrides.targets.<name>.ssh.passphrase, or run from a terminal to be asked.");
        }

        for (var attempt = 1; attempt <= PassphraseAttempts; attempt++)
        {
            var entered = prompt(keyPath, attempt);
            if (string.IsNullOrEmpty(entered))
                break;

            try
            {
                return new PrivateKeyFile(keyPath, entered);
            }
            catch (SshException)
            {
                // Wrong passphrase; the prompt says so on the next round.
            }
        }

        throw new CommandHostException($"Could not unlock {keyPath}.");
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

        var key = LoadKey(keyPath, config.Passphrase);

        return new ConnectionInfo(
            config.Host,
            config.Port,
            config.User,
            new PrivateKeyAuthenticationMethod(config.User, key));
    }
}
