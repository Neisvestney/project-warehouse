using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Services;

/// <summary>What of an order decides whose assembly worklist shows it.</summary>
public record AssemblyOrderState(bool InAssembly, IReadOnlySet<Guid> Assignees, IReadOnlySet<Guid> TaskIds)
{
    public static readonly AssemblyOrderState Absent = new(false, new HashSet<Guid>(), new HashSet<Guid>());
}

/// <summary>
/// Publishes <see cref="AssemblyChangedPayload"/>. A mutation captures the state of the orders it is about to
/// touch, and publishing compares it with the state afterwards — that comparison is what tells a change inside
/// an order apart from the order entering or leaving somebody's list.
/// </summary>
public interface IAssemblyChangeNotifier
{
    Task<IReadOnlyDictionary<Guid, AssemblyOrderState>> CaptureAsync(IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct = default);

    /// <param name="scope">What changed as far as the caller knows; raised to <see cref="AssemblyChangeScope.List"/>
    /// when the order's worklist membership changed, lowered to <see cref="AssemblyChangeScope.Order"/> when
    /// <paramref name="taskId"/> no longer exists.</param>
    Task PublishAsync(IReadOnlyDictionary<Guid, AssemblyOrderState> before, AssemblyChangeScope scope, Guid? taskId,
        HttpContext? httpContext, CancellationToken ct = default);
}
