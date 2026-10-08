using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

public class BatchRebindRequest
{
    // one transaction locks every order and rewrites its boxes; matches the label print limit, so a
    // selection that can be printed can be rebound too
    [MaxLength(1000)]
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
}
