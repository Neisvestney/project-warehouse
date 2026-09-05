namespace ProjectWarehouse.Ops.Services;

/// How a scenario tells the console what it is doing. A step that moves bytes reports them
/// cumulatively; a null total means the size only becomes known when the step ends.
public interface IStepReporter
{
    IStep Begin(string description, long? totalBytes = null);
}

/// A step is only shown as finished once <see cref="Complete"/> says so — disposing one that died
/// half way leaves it where it stopped rather than ticking off work that never happened.
public interface IStep : IProgress<long>, IDisposable
{
    void Complete();
}

public sealed class NullStepReporter : IStepReporter
{
    public static readonly NullStepReporter Instance = new();

    public IStep Begin(string description, long? totalBytes = null) => NullStep.Instance;

    private sealed class NullStep : IStep
    {
        public static readonly NullStep Instance = new();

        public void Report(long value)
        {
        }

        public void Complete()
        {
        }

        public void Dispose()
        {
        }
    }
}
