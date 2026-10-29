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
    Labels = 32,
    Returns = 64,
    Accruals = 128,
    Buyouts = 256,
    Fbo = 512,
    Supplies = 1024,

    /// <summary>A sold-then-returned unit comes as a cancelled posting; only the journal's reversed sale reveals it.</summary>
    ReversedSaleReturns = 2048,
}
