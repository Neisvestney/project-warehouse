using System.Text;
using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Infrastructure.Docker;

/// <param name="ComposeService">
/// Null for a container this compose project does not own, which is also a container the tool
/// cannot stop.
/// </param>
public sealed record VolumeUser(string Container, string? ComposeService);

/// <summary>Which files inside a volume a transfer covers.</summary>
/// <param name="SinceDays">Only files modified within this many days, or null for any age.</param>
/// <param name="NamePatterns">
/// Shell globs matched against the file name, any one of which is enough. Null for every file.
/// They are built from fixed catalogues rather than from what the user typed, so nothing here has
/// to survive a hostile pattern.
/// </param>
public sealed record VolumeFilter(
    int? SinceDays = null, IReadOnlyList<string>? NamePatterns = null)
{
    public static readonly VolumeFilter All = new();

    /// Empty is refused rather than read as null: "no restriction" and "selects nothing" would
    /// otherwise be the same value, and the one that means nothing is the one that would quietly
    /// transfer the whole volume. Constructing and `with` both go through the same check — a
    /// property initializer writes the field directly and would skip the accessor.
    public IReadOnlyList<string>? NamePatterns
    {
        get;
        init => field = Checked(value);
    } = Checked(NamePatterns);

    public bool CoversEverything => SinceDays is null && NamePatterns is null;

    private static IReadOnlyList<string>? Checked(IReadOnlyList<string>? patterns) =>
        patterns is { Count: 0 }
            ? throw new ArgumentException(
                "A filter with no name patterns cannot be told apart from an unfiltered one. "
                    + "Pass null to cover every file.",
                nameof(patterns))
            : patterns;
}

/// <param name="Bytes">
/// What the files hold. A floor rather than the transfer's size — the tar adds a header per file,
/// and compression takes far more than that back off again.
/// </param>
public sealed record VolumeContents(int Files, long Bytes)
{
    /// Null rather than zero, because a bar cannot be drawn against a total of nothing.
    public long? BarTotal => Bytes > 0 ? Bytes : null;
}

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

    /// Streams the selection out uncompressed, so nothing is staged on the target's disk. The size
    /// only becomes known once the last byte has arrived.
    public Task<CommandResult> ArchiveAsync(
        string volume,
        VolumeFilter filter,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken) =>
        target.Host.RunStreamingAsync(
            filter.CoversEverything
                ? ShellCommand.Of(
                    "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                    "tar", "-C", "/src", "-cf", "-", ".")
                : ShellCommand.Of(
                    "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                    "sh", "-c", $"set -o pipefail; cd /src && {Tar(filter)}"),
            destination,
            progress,
            cancellationToken);

    /// <summary>
    /// Compresses the selection into a file on the target and answers its exact size.
    /// </summary>
    /// <remarks>
    /// Costs a file on the target's disk — a tenth of what the selection holds, for text — and buys
    /// back the one thing a compressed stream cannot give: how many bytes the transfer will be
    /// before it starts. Gzip runs once either way, so the size is not paid for with a second pass.
    /// </remarks>
    public async Task<long> StageAsync(
        string volume,
        VolumeFilter filter,
        string stagingDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var staged = $"/out/{fileName}";

        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm",
                "-v", $"{volume}:/src:ro",
                "-v", $"{stagingDirectory}:/out",
                ToolImage,
                "sh", "-c",
                $"set -o pipefail; cd /src && {Tar(filter)} | gzip -c > {staged} "
                    + $"&& stat -c %s {staged}"),
            cancellationToken);

        if (!result.Succeeded)
            throw new CommandHostException($"Compressing {volume} failed: {result.FailureMessage}");

        return long.TryParse(result.StdOut.Trim(), out var bytes)
            ? bytes
            : throw new CommandHostException(
                $"Compressing {volume} reported no size: {result.StdOut.Trim()}");
    }

    /// Reads back what <see cref="StageAsync"/> wrote, through the same throwaway container the
    /// rest of this class uses — a target reached over SSH has no shell utilities this tool relies
    /// on, and a local one is not even POSIX.
    public Task<CommandResult> FetchStagedAsync(
        string stagingDirectory,
        string fileName,
        Stream destination,
        IProgress<long>? progress,
        CancellationToken cancellationToken) =>
        target.Host.RunStreamingAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{stagingDirectory}:/out:ro", ToolImage,
                "cat", $"/out/{fileName}"),
            destination,
            progress,
            cancellationToken);

    /// <returns>
    /// What the filter selects, or null when it could not be read — which is not the same answer as
    /// an empty selection, and must not be turned into one.
    /// </returns>
    public async Task<VolumeContents?> MeasureAsync(
        string volume, VolumeFilter filter, CancellationToken cancellationToken)
    {
        var result = await target.Host.RunAsync(
            ShellCommand.Of(
                "docker", "run", "--rm", "-v", $"{volume}:/src:ro", ToolImage,
                "sh", "-c",
                // pipefail because awk prints a clean "0 0" and exits zero over a find that died,
                // and an unreadable volume would otherwise be indistinguishable from an empty one —
                // which is exactly the difference the caller shortcuts on.
                $"set -o pipefail; cd /src && {Find(filter)} -exec stat -c %s {{}} + "
                    + "| awk '{ files++; total += $1 } END { print files + 0, total + 0 }'"),
            cancellationToken);

        if (!result.Succeeded)
            return null;

        var counts = result.StdOut.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return counts is [var files, var bytes]
            && int.TryParse(files, out var count)
            && long.TryParse(bytes, out var size)
                ? new VolumeContents(count, size)
                : null;
    }

    /// The tar half of every archive command. Runs in busybox's own shell inside the throwaway
    /// container — nothing built here reaches a shell on the host. Callers prefix `set -o pipefail`
    /// so that a tar dying mid-pipe is not masked by whatever exits cleanly after it.
    private static string Tar(VolumeFilter filter) =>
        filter.CoversEverything ? "tar -cf - ." : $"{Find(filter)} | tar -cf - -T -";

    /// The one predicate every read of a volume shares, so the size a bar is drawn against and the
    /// files that actually move can never select differently.
    private static string Find(VolumeFilter filter)
    {
        var find = new StringBuilder("find . -type f");

        if (filter.SinceDays is { } days)
            find.Append(" -mtime -").Append(days);

        if (filter.NamePatterns is { Count: > 0 } patterns)
        {
            // Quoted so the container's shell hands the glob to find rather than expanding it
            // against the volume itself.
            find.Append(" \\( ")
                .AppendJoin(" -o ", patterns.Select(pattern => $"-name \"{pattern}\""))
                .Append(" \\)");
        }

        return find.ToString();
    }

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
