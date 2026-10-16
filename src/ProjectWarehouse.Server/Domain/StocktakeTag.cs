namespace ProjectWarehouse.Server.Domain;

public class StocktakeTag : Tag
{
    public ICollection<Stocktake> Stocktakes { get; set; } = [];
}
