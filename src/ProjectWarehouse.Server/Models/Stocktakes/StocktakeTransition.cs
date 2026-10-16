namespace ProjectWarehouse.Server.Models.Stocktakes;

public enum StocktakeTransition
{
    Schedule = 0,
    ToDraft = 1,
    Start = 2,
    Revert = 3,
    Finish = 4,
    Cancel = 5,
}
