using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Infrastructure.Docker;

/// <param name="ComposeService">
/// Null for a container this compose project does not own, which is also a container the tool
/// cannot stop.
/// </param>
public sealed record VolumeUser(string Container, string? ComposeService);

/// Volume contents move as an uncompressed tar streamed through a throwaway container, so nothing
/// is ever staged on the target's disk.
public sealed class DockerVolumes(TargetContext target)
{
    private const string ToolImage = "busybox:1.37.0";

    /// The `docker run -v` source a logical volume maps to. A bind path is already one; a compose
    /// volume name has to be matched against what compose actually created.
    public Task<string> ResolveAsync(VolumeSource source, CancellationToken cancellationToken) =>
        source.Path is { } path
            ? Task.FromResult(path)
            : ResolveComposeVolumeAsync(source.Volume ?? string.Empty, cancellationToken);

    /// Compose prefixes a volume with its project name, and the project name depends on where the
    /// compose file lives. Matching by suffix avoids having to reproduce that rule.
    private async Task<string> ResolveComposeVolumeAsync(
        string composeVolume, CancellationToken cancellationToken)
    {
        var listed = await target.Host.RunAsync(
            ShellCommand.Of("docker", "volume", "ls", "--format", "{{.Name}}"), cancellationToken);

        if (!listed.Succeeded)
            throw new CommandHostException($"docker volume ls failed: {listed.FailureMessage}");

        var names = listed.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToList();

        if (names.Contains(composeVolume, StringComparer.Ordinal))
            return composeVolume;

        var suffix = "_" + composeVolume;
        var matches = names.Where(name => name.EndsWith(suffix, StringComparison.Ordinal)).ToList();

        return matches switch
        {
            [var only] => only,
            [] => throw new CommandHostException(
                $"No docker volume matching '{composeVolume}' on {target.Name}."),
            _ => throw new CommandHostException(
                $"'{composeVolume}' matches several volumes on {target.Name}: {string.Join(", ", matches)}."),
        };
    }

    public Task<CommandResult> ArchiveAsync(
        string volume, Stream destination, IProgress<long>? progress, CancellationToken cancellationToken) =>
        target.Host.RunStreamingAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                "tar", "-C", "/src", "-cf", "-", "."),
            destination,
            progress,
            cancellationToken);

    /// <param name="sinceDays">Same window the archive commands take, or null for the whole volume.</param>
    /// <returns>
    /// The bytes the files hold, or null when the size could not be read. A floor rather than the
    /// tar's size — the archive adds a header per file — and only ever used to draw a bar.
    /// </returns>
    public async Task<long?> MeasureAsync(
        string volume, int? sinceDays, CancellationToken cancellationToken)
    {
        var window = sinceDays is { } days ? $" -mtime -{days}" : string.Empty;

        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                "sh", "-c",
                $"cd /src && find .{window} -type f -exec stat -c %s {{}} + "
                    + "| awk '{ total += $1 } END { print total + 0 }'"),
            cancellationToken);

        if (!result.Succeeded)
            return null;

        return long.TryParse(result.StdOut.Trim(), out var bytes) && bytes > 0 ? bytes : null;
    }

    /// How many files the same window selects. Asked separately because busybox tar fails outright
    /// on an empty file list, and "nothing was written that day" is an answer, not an error.
    public async Task<int> CountRecentAsync(
        string volume, int sinceDays, CancellationToken cancellationToken)
    {
        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                "sh", "-c", $"cd /src && find . -type f -mtime -{sinceDays} | wc -l"),
            cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"Listing {volume} failed: {result.FailureMessage}");

        return int.TryParse(result.StdOut.Trim(), out var count) ? count : 0;
    }

    /// <param name="sinceDays">
    /// Only files modified within this many days. The pipeline runs in busybox's own shell inside
    /// the throwaway container — nothing here reaches a shell on the host.
    /// </param>
    public Task<CommandResult> ArchiveRecentAsync(
        string volume,
        int sinceDays,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken) =>
        target.Host.RunStreamingAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                "sh", "-c", $"cd /src && find . -type f -mtime -{sinceDays} | tar -cf - -T -"),
            destination,
            progress,
            cancellationToken);

    /// Running containers that have the volume mounted. Emptying a volume under a process that is
    /// holding files open in it is how a restore turns into corruption.
    public async Task<IReadOnlyList<VolumeUser>> UsersAsync(
        string volume, CancellationToken cancellationToken)
    {
        // `--filter volume=` matches a volume name or a mount point inside the container, never a
        // bind's source on the host, so a bind is found by reading every container's mounts.
        var command = IsBind(volume)
            ? ShellCommand.Of(
                "docker", "ps", "--no-trunc",
                "--format", "{{.Names}}|{{.Label \"com.docker.compose.service\"}}|{{.Mounts}}")
            : ShellCommand.Of(
                "docker", "ps", "--filter", $"volume={volume}",
                "--format", "{{.Names}}|{{.Label \"com.docker.compose.service\"}}");

        var result = await target.Host.RunAsync(command, cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"docker ps failed: {result.FailureMessage}");

        var users = new List<VolumeUser>();

        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|', 3);
            if (parts[0].Length == 0)
                continue;

            if (IsBind(volume) && !MountsInclude(parts.Length > 2 ? parts[2] : string.Empty, volume))
                continue;

            var service = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null;
            users.Add(new VolumeUser(parts[0], service));
        }

        return users;
    }

    /// A volume name cannot hold a path separator, so this tells the two mount kinds apart.
    private static bool IsBind(string mount) => mount.Contains('/') || mount.Contains('\\');

    /// Docker reports a bind source as the daemon sees it, which on Docker Desktop is the path
    /// inside its VM (`/run/desktop/mnt/host/f/...`) rather than the `F:\...` it was given. A
    /// drive-lettered path is therefore matched on its drive-relative tail, not on the whole string.
    private static bool MountsInclude(string mounts, string path)
    {
        var wanted = Normalize(path);
        var tail = DriveRelative(wanted);

        return mounts.Split(',').Any(
            mount => Normalize(mount) is var reported
                && (reported == wanted || (tail is not null && reported.EndsWith(tail, StringComparison.Ordinal))));
    }

    private static string? DriveRelative(string normalized) =>
        normalized.Length > 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'
            ? "/" + normalized[0] + normalized[2..]
            : null;

    private static string Normalize(string path) =>
        path.Trim().Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

    /// Reads the archive end to end before anything is destroyed. A truncated tar would otherwise
    /// be discovered only after the volume had been emptied, with nothing left to put back.
    public async Task VerifyAsync(
        string hostDirectory, string archiveName, CancellationToken cancellationToken)
    {
        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{hostDirectory}:/backup:ro", ToolImage,
                "tar", "-tf", $"/backup/{archiveName}"),
            cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"{archiveName} is not a readable tar: {result.FailureMessage}");
    }

    /// Clears the volume before extracting: a restore that merged into what is already there would
    /// leave files no backup ever contained.
    public async Task RestoreAsync(
        string volume, string hostDirectory, string archiveName, CancellationToken cancellationToken)
    {
        await VerifyAsync(hostDirectory, archiveName, cancellationToken);

        var clear = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/dst", ToolImage,
                "find", "/dst", "-mindepth", "1", "-delete"),
            cancellationToken);

        if (!clear.Succeeded)
            throw new CommandHostException($"Could not clear volume {volume}: {clear.FailureMessage}");

        var extract = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm",
                "-v", $"{volume}:/dst",
                "-v", $"{hostDirectory}:/backup:ro",
                ToolImage,
                "tar", "-C", "/dst", "-xf", $"/backup/{archiveName}"),
            cancellationToken);

        if (!extract.Succeeded)
            throw new CommandHostException($"Could not restore volume {volume}: {extract.FailureMessage}");
    }
}
