using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class UpdateStocktakeRequest
{
    [StringLength(256)]
    public string? Name { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }
}
