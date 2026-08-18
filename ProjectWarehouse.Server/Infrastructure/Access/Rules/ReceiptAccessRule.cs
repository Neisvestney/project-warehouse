using System.Security.Claims;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Access.Rules;

/// <summary>
/// Receipts add one wrinkle to the standard warehouse scope: <c>receipts.process_assigned</c> on its own
/// is a warehouse operator's permission, and it only exposes receipts that are actually being processed.
/// A broader view permission overrides that narrowing.
/// </summary>
public class ReceiptAccessRule(ApplicationDbContext db, AccessScope scope)
    : WarehouseScopedRule<Receipt>(
        db, scope, AppEntityType.Receipt,
        viewAll: [Permissions.Receipts.View],
        viewAssigned: [Permissions.Receipts.ViewAssigned, Permissions.Receipts.ProcessAssigned],
        editAll: [Permissions.Receipts.Edit],
        editAssigned: [Permissions.Receipts.EditAssigned],
        warehouse: r => r.WarehouseId,
        ErrorCode.ReceiptNotAssignedToWarehouse,
        "You are not assigned to the warehouse of this receipt.")
{
    public override async Task<IQueryable<Receipt>> QueryAsync(
        ClaimsPrincipal user, AccessLevel level, CancellationToken ct = default)
    {
        var query = await base.QueryAsync(user, level, ct);

        return ProcessingOnly(user, level)
            ? query.Where(r => r.Status == ReceiptStatus.Processing)
            : query;
    }

    public override async Task<AccessVerdict> CheckAsync(
        ClaimsPrincipal user, AccessLevel level, Receipt entity, CancellationToken ct = default)
    {
        var verdict = await base.CheckAsync(user, level, entity, ct);

        if (verdict.Allowed && ProcessingOnly(user, level) && entity.Status != ReceiptStatus.Processing)
            return AccessVerdict.NoPermission;

        return verdict;
    }

    /// <summary>True when the only thing granting access is <c>process_assigned</c>.</summary>
    private static bool ProcessingOnly(ClaimsPrincipal user, AccessLevel level) =>
        level == AccessLevel.View
        && AccessScope.Has(user, Permissions.Receipts.ProcessAssigned)
        && !AccessScope.Has(user, Permissions.Receipts.View)
        && !AccessScope.Has(user, Permissions.Receipts.ViewAssigned);
}
