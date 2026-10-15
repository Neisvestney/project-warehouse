using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ProjectWarehouse.Ops.Infrastructure;

/// A streamed command hashes what it writes on the target and reports the SHA-256 on stderr. An
/// exit code only speaks for the producer; the hash also covers every hop between it and the file.
public static partial class StreamChecksum
{
    /// Tees the producer's stdout through `sha256sum`, whose line goes to stderr. Callers set
    /// `pipefail` first, or a producer dying mid-stream is masked by the hash exiting cleanly.
    /// The hash is printed after a newline once the stream has ended, so it never lands glued to a
    /// producer's stderr that stopped mid-line.
    public static string Wrap(string producer) =>
        $"{{ {{ {producer} | tee /dev/fd/3; }} 3>&1 1>&4 "
            + $"| {{ h=$(sha256sum) && printf \"\\n%s\\n\" \"$h\" >&2; }}; }} 4>&1";

    /// The reported hash, and stderr without its line so a failure message shows only the producer's.
    public static (string? Sha256, string StdErr) Split(string stdErr)
    {
        var lines = stdErr.Replace("\r", string.Empty).Split('\n').ToList();
        var index = lines.FindLastIndex(line => HashLine().IsMatch(line));

        if (index < 0)
            return (null, stdErr);

        var sha256 = HashLine().Match(lines[index]).Groups[1].Value;
        lines.RemoveAt(index);
        return (sha256, string.Join('\n', lines));
    }

    public static bool Matches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    public static async Task<string> OfFileAsync(
        string path, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await file.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            total += read;
            progress?.Report(total);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    [GeneratedRegex(@"^([0-9a-f]{64})\s+-$")]
    private static partial Regex HashLine();
}
