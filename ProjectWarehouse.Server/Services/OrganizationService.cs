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
    IEntityLockService locks,
    IMapper mapper,
    IChangeLogService<OrganizationDto> changeLog) : IOrganizationService
{
    public async Task LinkByInnAsync(MarketplaceAccount account, CancellationToken ct, TimeSpan? lockTimeout = null)
    {
        if (account.IsOrganizationLinkedManually)
            return;

        // A blank INN from the marketplace is no reason to drop a link that was right on the last run
        var inn = account.Inn?.Trim();
        if (string.IsNullOrEmpty(inn))
            return;

        var existing = await FindLockedByInnAsync(inn, lockTimeout, ct);
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
            // null only if it was deleted again meanwhile; the next sync links the account anew
            account.Organization = await FindLockedByInnAsync(inn, lockTimeout, ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        await changeLog.CompareAndSaveToChangelog(null, await GetDtoAsync(organization.Id, ct),
            OrganizationActions.AutoCreated, new { accountId = account.Id });
    }

    /// <summary>
    /// Locked before it is read: a delete of the organization holds FOR UPDATE, and linking to a row that is
    /// about to vanish would fail on the foreign key. Once the lock is ours a deleted row simply reads as absent.
    /// </summary>
    private async Task<Organization?> FindLockedByInnAsync(string inn, TimeSpan? lockTimeout, CancellationToken ct)
    {
        var id = await db.Organizations.Where(o => o.Inn == inn).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
        if (id is null)
            return null;

        await locks.LockAsync<Organization>(id.Value, ct, lockTimeout);
        return await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<OrganizationDto?> GetDtoAsync(Guid id, CancellationToken ct) =>
        db.Organizations
            .Where(o => o.Id == id)
            .ProjectTo<OrganizationDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
}
