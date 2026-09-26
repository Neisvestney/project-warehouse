using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Services;

public class AssemblyChangeNotifier(ApplicationDbContext db, IRealtimeNotifier realtime) : IAssemblyChangeNotifier
{
    public async Task<IReadOnlyDictionary<Guid, AssemblyOrderState>> CaptureAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, AssemblyOrderState>();

        var rows = await db.Orders
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new
            {
                o.Id,
                o.Status,
                Tasks = o.AssemblyTasks.Select(t => new { t.Id, t.AssignedToId }).ToList(),
            })
            .ToListAsync(ct);

        var states = rows.ToDictionary(r => r.Id, r => new AssemblyOrderState(
            r.Status == OrderStatus.Assembly,
            r.Tasks.Select(t => t.AssignedToId).OfType<Guid>().ToHashSet(),
            r.Tasks.Select(t => t.Id).ToHashSet()));

        // a deleted order still has to be announced to whoever had it on the list
        foreach (var id in orderIds)
            states.TryAdd(id, AssemblyOrderState.Absent);

        return states;
    }

    public async Task PublishAsync(IReadOnlyDictionary<Guid, AssemblyOrderState> before, AssemblyChangeScope scope,
        Guid? taskId, HttpContext? httpContext, CancellationToken ct = default)
    {
        var after = await CaptureAsync(before.Keys.ToList(), ct);

        var byUserId = EntityChangedEvents.GetUserId(httpContext?.User);
        var byUserName = httpContext?.User.GetDisplayName();
        var exceptConnectionId = EntityChangedEvents.GetConnectionId(httpContext);

        foreach (var (orderId, was) in before)
        {
            var now = after[orderId];

            var recipients = new HashSet<Guid>();
            if (was.InAssembly) recipients.UnionWith(was.Assignees);
            if (now.InAssembly) recipients.UnionWith(now.Assignees);
            if (recipients.Count == 0)
                continue;

            var membershipChanged = was.InAssembly != now.InAssembly || !was.Assignees.SetEquals(now.Assignees);
            var effective = membershipChanged ? AssemblyChangeScope.List
                : scope == AssemblyChangeScope.Task && (taskId is null || !now.TaskIds.Contains(taskId.Value))
                    ? AssemblyChangeScope.Order
                    : scope;

            await realtime.PublishAsync(
                RealtimeAddress.ToWatchers(AppEntityType.OrderAssembly, Guid.Empty,
                    exceptConnectionId: exceptConnectionId, onlyUserIds: recipients),
                new RealtimeEvent
                {
                    Payload = new AssemblyChangedPayload
                    {
                        Scope = effective,
                        OrderId = orderId,
                        TaskId = effective == AssemblyChangeScope.Task ? taskId : null,
                        ByUserId = byUserId,
                        ByUserName = byUserName,
                    },
                }, ct);
        }
    }
}
