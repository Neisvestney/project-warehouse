using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class CreateStoragePlaceNodeRequest
{
    [Required, MinLength(1)]
    public string Name { get; init; } = null!;
    public Guid? ParentNodeId { get; init; }
}