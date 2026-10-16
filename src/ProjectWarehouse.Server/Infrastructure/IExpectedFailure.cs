namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Marks an exception as a business outcome rather than a fault: the request is rejected, the caller turns it
/// into a 4xx, and nothing is broken. Telemetry uses it to keep such rollbacks out of the error rate — an
/// unmarked exception is treated as a genuine failure, so the safe move is to leave a new type unmarked.
/// <para>
/// Deliberately not applied to <see cref="InventoryWriteConflictException" />: an exhausted retry budget is an
/// operational problem worth alerting on.
/// </para>
/// </summary>
public interface IExpectedFailure
{
    /// <summary>
    /// The code the caller reports, when the type alone does not identify the refusal. Left unset by types
    /// that are already specific enough on their own — the span carries their name either way.
    /// </summary>
    ErrorCode? Code => null;
}
