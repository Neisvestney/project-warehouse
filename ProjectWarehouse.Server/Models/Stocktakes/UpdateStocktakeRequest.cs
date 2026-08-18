using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class UpdateStocktakeRequest
{
    [StringLength(256)]
    public string? Name { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }

    /// <summary>Omit to leave the planning untouched; sending it rewrites both type and planned date.</summary>
    public StocktakeType? Type { get; init; }

    /// <summary>Required when the type is Scheduled, ignored otherwise.</summary>
    public DateOnly? PlannedDate { get; init; }
}
