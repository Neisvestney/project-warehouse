using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Catalog;

public class BundleComponentRequest
{
    public Guid? Id { get; init; }

    public Guid ComponentId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
