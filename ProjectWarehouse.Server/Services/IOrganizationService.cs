using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Organizations;

namespace ProjectWarehouse.Server.Services;

public interface IOrganizationService
{
    /// <summary>
    /// Links the account to the organization with the account's INN, creating one from its seller details
    /// when none exists, and saves. No-op for a manually linked account or one without an INN.
    /// </summary>
    Task LinkByInnAsync(MarketplaceAccount account, CancellationToken ct);

    Task<OrganizationDto?> GetDtoAsync(Guid id, CancellationToken ct);
}
