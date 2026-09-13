using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class CreateStocktakeTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
