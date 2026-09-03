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

    /// Compose prefixes a volume with its project name, and the project name depends on where the
    /// compose file lives. Matching by suffix avoids having to reproduce that rule.
    public async Task<string> ResolveAsync(string composeVolume, CancellationToken cancellationToken)
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
        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "ps", "--filter", $"volume={volume}",
                "--format", "{{.Names}}|{{.Label \"com.docker.compose.service\"}}"),
            cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"docker ps failed: {result.FailureMessage}");

        var users = new List<VolumeUser>();

        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|', 2);
            if (parts[0].Length == 0)
                continue;

            var service = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null;
            users.Add(new VolumeUser(parts[0], service));
        }

        return users;
    }

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
