using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models;

/// <summary>
/// Why a posting did not become an order. Lives in Models for the same reason as
/// <see cref="AppFieldError"/>: the entity stores it as jsonb and the DTO hands it out unchanged.
/// </summary>
public class SkippedOrderInfo
{
    public string PostingNumber { get; set; } = null!;

    /// <summary>Same vocabulary as <see cref="AppFieldError.Code"/> — persisted as a number, so codes are never renumbered.</summary>
    public ErrorCode Reason { get; set; }

    /// <summary>Seller articles that caused the skip; empty when the warehouse was the problem.</summary>
    public IList<string> OfferIds { get; set; } = [];
}
