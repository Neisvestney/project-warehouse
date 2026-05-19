using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.InboundOrders;

public class DraftItemsGroupItem
{
    public Guid? Id { get; init; }
    [Required, MinLength(1)] public string Name { get; init; } = null!;
    [Required, MinLength(1)] public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public string? RootBarcode { get; init; }
    [Required, MinLength(1)] public string Characteristic { get; init; } = null!;
    [Range(1, int.MaxValue)] public int Count { get; init; }
    public Guid? CatalogItemId { get; init; }
    public Guid? CatalogItemWithCharacteristicId { get; init; }
    public bool CreateNew { get; init; }
}

public class UpdateDraftItemsGroupsRequest
{
    [Required] public IReadOnlyList<DraftItemsGroupItem> DraftItemsGroups { get; init; } = [];
}
