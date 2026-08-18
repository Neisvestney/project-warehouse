namespace ProjectWarehouse.Server.Domain;

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Assembly = 2,
    Assembled = 3,
    Shipped = 4,
    Canceled = 5,
}
