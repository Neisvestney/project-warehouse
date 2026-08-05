namespace ProjectWarehouse.Server.Integrations.Abstractions;

[Flags]
public enum MarketplaceCapabilities
{
    None = 0,
    Warehouses = 1,
    Cards = 2,
    Orders = 4,
    StockPush = 8,
    SellerInfo = 16,
}
