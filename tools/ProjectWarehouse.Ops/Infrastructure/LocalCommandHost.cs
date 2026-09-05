using System.Diagnostics;
using System.Text;

namespace ProjectWarehouse.Ops.Infrastructure;

public sealed class LocalCommandHost(string workingDirectory) : ICommandHost
{
    public string Description => $"local ({workingDirectory})";

    public async Task<CommandResult> RunAsync(ShellCommand command, CancellationToken cancellationToken)
    {
        using var process = Start(command, redirectStdOut: true);

        var stdOut = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stdErr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Left alone, the child keeps running detached and goes on mutating the target after
            // the tool has exited.
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw;
        }

        return new CommandResult(process.ExitCode, await stdOut, await stdErr);
    }

    public async Task<CommandResult> RunStreamingAsync(
        ShellCommand command,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        using var process = Start(command, redirectStdOut: true);
        var stdErr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await CopyWithProgressAsync(
                process.StandardOutput.BaseStream, destination, progress, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A pg_dump left running holds its transaction snapshot open long after pwops is gone.
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            await stdErr;
            throw;
        }

        return new CommandResult(process.ExitCode, string.Empty, await stdErr);
    }

    /// Runs with this process's console handles. Nothing is captured, and that is the point:
    /// docker only draws its progress display when it is talking to a terminal.
    public async Task<int> RunInheritedAsync(ShellCommand command, CancellationToken cancellationToken)
    {
        using var process = Start(command, redirectStdOut: false, redirectStdErr: false);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw;
        }

        return process.ExitCode;
    }

    public async Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;

    public Task WriteFileAsync(string path, string content, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, content, cancellationToken);

    public async Task ReplaceFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".pwops.tmp";
        await File.WriteAllTextAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    /// Windows files inherit their ACL from the directory, so there is nothing to tighten here.
    public Task ProtectFileAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task UploadFileAsync(
        string localPath,
        string remotePath,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(localPath);
        await using var destination = File.Create(remotePath);
        await CopyWithProgressAsync(source, destination, progress, cancellationToken);
    }

    public Task<string> CreateTempDirectoryAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pwops-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return Task.FromResult(path);
    }

    public Task RemoveDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
            progress?.Report(total);
        }
    }

    private Process Start(ShellCommand command, bool redirectStdOut, bool redirectStdErr = true)
    {
        var info = new ProcessStartInfo
        {
            FileName = command.Executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = redirectStdOut,
            RedirectStandardError = redirectStdErr,
            UseShellExecute = false,
            StandardOutputEncoding = redirectStdOut ? Encoding.UTF8 : null,
            StandardErrorEncoding = redirectStdErr ? Encoding.UTF8 : null,
        };

        foreach (var arg in command.Args)
            info.ArgumentList.Add(arg);

        try
        {
            return Process.Start(info)
                ?? throw new CommandHostException($"Could not start '{command.Executable}'.");
        }
        catch (Exception ex) when (ex is not CommandHostException)
        {
            throw new CommandHostException($"Could not start '{command.Executable}': {ex.Message}", ex);
        }
    }
}
