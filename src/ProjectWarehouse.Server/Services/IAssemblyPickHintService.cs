using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

/// <summary>Variant choices to start an assembly from, taken from the user's own previous fulfillments.</summary>
public interface IAssemblyPickHintService
{
    /// <summary>
    /// One hint per requested item and warehouse that <paramref name="userId"/> (anyone when null) has assembled
    /// there before and whose choices still fit the current catalog. Cells are never hinted — the warehouse
    /// default applies.
    /// </summary>
    Task<IReadOnlyList<AssemblyLastPickDto>> GetLastPicksAsync(
        Guid? userId, IReadOnlyList<AssemblyLastPicksItemRequest> items, CancellationToken ct = default);
}
