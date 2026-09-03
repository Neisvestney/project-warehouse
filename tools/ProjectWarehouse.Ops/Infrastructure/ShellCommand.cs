using System.Text;

namespace ProjectWarehouse.Ops.Infrastructure;

/// Argv rather than a shell line: the local host spawns the process directly and the remote host
/// quotes the arguments itself, so nothing depends on which shell happens to be on the far side.
public sealed record ShellCommand(string Executable, IReadOnlyList<string> Args)
{
    public static ShellCommand Of(string executable, params string[] args) =>
        new(executable, args);

    public ShellCommand With(params string[] extra) =>
        this with { Args = [.. Args, .. extra] };

    public string ToPosixLine()
    {
        var builder = new StringBuilder(QuotePosix(Executable));
        foreach (var arg in Args)
            builder.Append(' ').Append(QuotePosix(arg));

        return builder.ToString();
    }

    public override string ToString() => $"{Executable} {string.Join(' ', Args)}";

    private static string QuotePosix(string value)
    {
        if (value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || "-_./:=@,+".Contains(c)))
            return value;

        return $"'{value.Replace("'", "'\\''")}'";
    }
}

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Succeeded => ExitCode == 0;

    public string FailureMessage =>
        string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : StdErr.Trim();
}
