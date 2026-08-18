using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Transfers;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/transfers")]
public class TransfersController(
    ApplicationDbContext db,
    AccessScope scope,
    IInventoryService inventory) : AppControllerBase
{
    /// <summary>Execute an atomic inventory transfer between two storage nodes.</summary>
    /// <remarks>
    /// All items are moved in a single transaction — if any item fails, the entire transfer is rolled back.
    /// Transfer type is determined by which field of each <see cref="TransferItemRequest"/> is populated:
    /// <c>catalogItemId + count</c> → Standard; <c>unitItemId</c> → Unit.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Execute(
        [FromBody] ExecuteTransferRequest request,
        CancellationToken ct = default)
    {
        // Transfers have their own permission family and no entity of their own, so there is no access rule —
        // only the assigned-warehouse narrowing is shared.
        if (!AccessScope.Has(User, Permissions.Transfers.Execute)
            && !AccessScope.Has(User, Permissions.Transfers.ExecuteAssigned))
            return Forbidden();

        if (request.FromNodeId == request.ToNodeId)
            return UnprocessableEntity("fromNodeId", ErrorCode.TransferSameNode,
                "Source and destination nodes must be different.");

        if (request.Items.Count == 0)
            return UnprocessableEntity("items", ErrorCode.ValidationError,
                "At least one item is required.");

        // Validate item requests
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var hasStandard = item.CatalogItemId.HasValue;
            var hasUnit     = item.UnitItemId.HasValue;

            var filledCount = (hasStandard ? 1 : 0) + (hasUnit ? 1 : 0);
            if (filledCount != 1)
                return UnprocessableEntity($"items[{i}]", ErrorCode.ValidationError,
                    "Each item must have exactly one of: catalogItemId or unitItemId.");

            if (hasStandard && (item.Count is null or <= 0))
                return UnprocessableEntity($"items[{i}].count", ErrorCode.ValidationError,
                    "Count must be greater than zero for standard items.");
        }

        // Verify nodes exist and resolve their warehouse IDs for permission check
        var fromNodeWarehouseId = await db.StoragePlacesNodes
            .Where(n => n.Id == request.FromNodeId)
            .Select(n => (Guid?)n.RootStoragePlace.WarehouseId)
            .FirstOrDefaultAsync(ct);

        if (fromNodeWarehouseId is null)
            return UnprocessableEntity("fromNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Source storage place node not found.");

        var toNodeWarehouseId = await db.StoragePlacesNodes
            .Where(n => n.Id == request.ToNodeId)
            .Select(n => (Guid?)n.RootStoragePlace.WarehouseId)
            .FirstOrDefaultAsync(ct);

        if (toNodeWarehouseId is null)
            return UnprocessableEntity("toNodeId", ErrorCode.StoragePlaceNodeNotFound,
                "Destination storage place node not found.");

        // For assigned-only users, both warehouses must be in their assigned set
        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Transfers.Execute, ct);
        if (AccessError(narrowing.Verdict) is { } error)
            return error;

        if (narrowing.Ids is { } assignedIds)
        {
            if (!assignedIds.Contains(fromNodeWarehouseId.Value))
                return Forbidden(ErrorCode.TransferNotAssignedToWarehouse,
                    "You are not assigned to the source warehouse.");

            if (!assignedIds.Contains(toNodeWarehouseId.Value))
                return Forbidden(ErrorCode.TransferNotAssignedToWarehouse,
                    "You are not assigned to the destination warehouse.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                foreach (var item in request.Items)
                {
                    if (item.CatalogItemId.HasValue)
                    {
                        await inventory.MoveStandardItemsAsync(
                            request.FromNodeId,
                            request.ToNodeId,
                            item.CatalogItemId.Value,
                            item.Count!.Value,
                            ct: ct);
                    }
                    else if (item.UnitItemId.HasValue)
                    {
                        await inventory.MoveUnitItemAsync(
                            item.UnitItemId.Value,
                            request.ToNodeId,
                            ct: ct);
                    }
                }

                await tx.CommitAsync(ct);
            });
        }
        catch (InsufficientInventoryException ex)
        {
            return UnprocessableEntity("items", ErrorCode.InsufficientInventory,
                $"Insufficient inventory: requested {ex.Requested}, available {ex.Available}.",
                ex.ToArgs());
        }
        catch (StoragePlaceNodeNotFoundException)
        {
            return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeNotFound,
                "Storage place node not found.");
        }
        catch (UnitInventoryItemNotFoundException ex)
        {
            return UnprocessableEntity("items", ErrorCode.UnitInventoryItemNotFound,
                $"Unit inventory item '{ex.ItemId}' not found.");
        }

        return NoContent();
    }
}
