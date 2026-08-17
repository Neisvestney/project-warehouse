using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class StocktakeItemRequest
{
    [JsonRequired]
    public StocktakeItemKind Kind { get; init; }

    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    /// <summary>Counted amount. For <see cref="StocktakeItemKind.Unit"/> lines only 0 or 1 is meaningful.</summary>
    [JsonRequired]
    public int CountedQuantity { get; init; }

    /// <summary>Required for unit lines, must be absent for standard ones.</summary>
    [StringLength(128, MinimumLength = 1)]
    public string? InventoryNumber { get; init; }

    /// <summary>Optional hint; the server re-resolves the serial by number anyway.</summary>
    public Guid? UnitInventoryItemId { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }
}
