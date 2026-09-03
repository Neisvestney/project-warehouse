namespace ProjectWarehouse.Ops.Infrastructure.Git;

public sealed record GitState(string Commit, string Subject, bool Dirty, string? StatusError)
{
    /// A tree whose state could not be read is not a clean tree.
    public bool SafeToDeploy => !Dirty && StatusError is null;
}

public sealed class GitClient(TargetContext target)
{
    public async Task<GitState?> ReadStateAsync(CancellationToken cancellationToken)
    {
        var log = await RunAsync(cancellationToken, "log", "-1", "--format=%h %s");
        if (!log.Succeeded)
            return null;

        // Untracked files are not a reason to refuse: they cannot conflict with a fast-forward,
        // and the target's own working directory collects them (.env.bak among others).
        var status = await RunAsync(cancellationToken, "status", "--porcelain", "--untracked-files=no");

        var line = log.StdOut.Trim();
        var separator = line.IndexOf(' ');

        return new GitState(
            separator < 0 ? line : line[..separator],
            separator < 0 ? string.Empty : line[(separator + 1)..],
            status.Succeeded && status.StdOut.Trim().Length > 0,
            status.Succeeded ? null : status.FailureMessage);
    }

    public Task<CommandResult> PullAsync(CancellationToken cancellationToken) =>
        RunAsync(cancellationToken, "pull", "--ff-only");

    private Task<CommandResult> RunAsync(CancellationToken cancellationToken, params string[] args) =>
        target.Host.RunAsync(
            ShellCommand.Of("git", "-C", target.RootDirectory).With(args), cancellationToken);
}
