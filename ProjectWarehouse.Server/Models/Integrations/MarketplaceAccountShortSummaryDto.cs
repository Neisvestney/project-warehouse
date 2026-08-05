using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceAccountShortSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public MarketplaceType Type { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }
}
