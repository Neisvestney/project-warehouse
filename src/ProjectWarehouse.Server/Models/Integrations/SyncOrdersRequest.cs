using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Integrations;

public class SyncOrdersRequest
{
    [MaxLength(50)]
    public IReadOnlyList<Guid> AccountIds { get; init; } = [];
}

/// <summary>
/// Partial success, same shape as the order batch endpoints: one rejected account must not sink the
/// other four the user ticked.
/// </summary>
/// <remarks>
/// The <c>marketplaceSyncAlreadyRunning</c> rejection can arrive two ways. The controller's cheap
/// pre-check puts it in <see cref="FailedItems"/>; the authoritative advisory lock in the worker
/// instead produces a started run that later ends up Failed with that code in its error. The UI has
/// to render both.
/// </remarks>
public class SyncOrdersResponse
{
    public IReadOnlyList<SyncOrdersStartedItem> Items { get; init; } = [];
    public IReadOnlyList<SyncOrdersFailedItem> FailedItems { get; init; } = [];
}

public class SyncOrdersStartedItem
{
    public Guid AccountId { get; init; }
    public Guid SyncRunId { get; init; }
}

public class SyncOrdersFailedItem
{
    public Guid AccountId { get; init; }
    public string? AccountName { get; init; }
    public AppFieldError Error { get; init; } = null!;
}
