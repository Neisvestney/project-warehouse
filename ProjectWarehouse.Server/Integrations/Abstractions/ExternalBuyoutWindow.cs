namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Buyouts of one report window, <paramref name="From"/> to <paramref name="To"/> inclusive. An empty window is
/// yielded too: that the window was read is what moves the account's buyout mark.
/// </summary>
public record ExternalBuyoutWindow(DateOnly From, DateOnly To, IReadOnlyList<ExternalAccrual> Accruals);
