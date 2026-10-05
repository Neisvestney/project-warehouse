namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// The entity lock was not granted within <c>lock_timeout</c>: another request is still writing the same
/// object. The transaction rolls back with nothing written, so the request is safe to repeat.
/// </summary>
public class EntityLockedException(string resource, Exception inner)
    : Exception($"Lock on '{resource}' was not granted in time.", inner), IExpectedFailure
{
    public string Resource { get; } = resource;

    ErrorCode? IExpectedFailure.Code => ErrorCode.EntityLocked;
}
