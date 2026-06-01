namespace ProjectWarehouse.Server.Infrastructure;

public class BundleTooManyCombinationsException(int limit)
    : Exception($"Bundle has too many possible assembly combinations (limit: {limit}).")
{
    public int Limit { get; } = limit;
}
