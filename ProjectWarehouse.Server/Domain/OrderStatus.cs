namespace ProjectWarehouse.Server.Domain;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Assembly,
    Assembled,
    Shipped,
    Canceled
}
