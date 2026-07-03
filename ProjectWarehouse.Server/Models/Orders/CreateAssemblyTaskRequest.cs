using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class CreateAssemblyTaskRequest
{
    public Guid? AssignedToId { get; init; }

    [JsonRequired]
    [MinLength(1)]
    public IReadOnlyList<CreateAssemblyTaskBoxRequest> Boxes { get; init; } = [];
}

public class CreateAssemblyTaskBoxRequest
{
    [JsonRequired]
    public Guid OrderBoxId { get; init; }

    [JsonRequired]
    [MinLength(1)]
    public IReadOnlyList<CreateAssemblyTaskBoxComponentRequest> Components { get; init; } = [];
}

public class CreateAssemblyTaskBoxComponentRequest
{
    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
