using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Organizations;

namespace ProjectWarehouse.Server.Services;

public class OrganizationService(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<OrganizationDto> changeLog) : IOrganizationService
{
    public async Task LinkByInnAsync(MarketplaceAccount account, CancellationToken ct)
    {
        if (account.IsOrganizationLinkedManually)
            return;

        // A blank INN from the marketplace is no reason to drop a link that was right on the last run
        var inn = account.Inn?.Trim();
        if (string.IsNullOrEmpty(inn))
            return;

        var existing = await db.Organizations.FirstOrDefaultAsync(o => o.Inn == inn, ct);
        if (existing is not null)
        {
            if (account.OrganizationId == existing.Id)
                return;

            account.Organization = existing;
            await db.SaveChangesAsync(ct);
            return;
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(account.CompanyLegalName) ? account.Name : account.CompanyLegalName,
            Inn = inn,
            LegalName = account.CompanyLegalName,
            Ogrn = account.Ogrn,
            OwnershipForm = account.OwnershipForm,
            CreatedAt = DateTime.UtcNow,
        };
        db.Organizations.Add(organization);
        account.Organization = organization;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (UniqueViolations.IsOrganizationInn(e))
        {
            // Another account's sync or an operator created it between the read and the insert
            db.Entry(organization).State = EntityState.Detached;
            account.Organization = await db.Organizations.FirstAsync(o => o.Inn == inn, ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        await changeLog.CompareAndSaveToChangelog(null, await GetDtoAsync(organization.Id, ct),
            OrganizationActions.AutoCreated, new { accountId = account.Id });
    }

    public Task<OrganizationDto?> GetDtoAsync(Guid id, CancellationToken ct) =>
        db.Organizations
            .Where(o => o.Id == id)
            .ProjectTo<OrganizationDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
}
