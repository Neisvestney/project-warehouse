using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Services;

public class StocktakeService(
    ApplicationDbContext db,
    IEntityLockService locks,
    IInventoryService inventory,
    IStocktakeDiffCalculator diffCalculator) : IStocktakeService
{
    public IQueryable<Stocktake> WithDetails(IQueryable<Stocktake> source, bool includeItems = false)
    {
        var q = source
            .Include(s => s.Warehouse)
            .Include(s => s.Tags)
            .Include(s => s.Images).ThenInclude(i => i.DataFile);

        return includeItems
            ? q
                .Include(s => s.Nodes)
                .ThenInclude(n => n.StoragePlaceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(s => s.Nodes)
                .ThenInclude(n => n.Items)
                .ThenInclude(i => i.CatalogItem)
                .ThenInclude(c => c.Group)
                .Include(s => s.Nodes)
                .ThenInclude(n => n.Items)
                .ThenInclude(i => i.UnitInventoryItem)
                .AsSplitQuery()
            : q.Include(s => s.Nodes).AsSplitQuery();
    }

    public async Task<AppProblemDetails?> FindNodeCountedElsewhereAsync(
        Guid stocktakeId, IReadOnlyCollection<Guid> nodeIds, CancellationToken ct = default)
    {
        if (nodeIds.Count == 0) return null;

        // Two stocktakes starting over the same cell would both read the other as not yet InProgress; the cells'
        // row locks make the second one wait for the first commit and then see it.
        await locks.LockManyAsync<StoragePlaceNode>(nodeIds, ct);

        var busy = await db.StocktakeNodes
            .Where(n => nodeIds.Contains(n.StoragePlaceNodeId)
                        && n.StocktakeId != stocktakeId
                        && n.Stocktake.Status == StocktakeStatus.InProgress)
            .OrderBy(n => n.StoragePlaceNode.Name)
            .Select(n => new { n.StoragePlaceNodeId, n.StoragePlaceNode.Name })
            .FirstOrDefaultAsync(ct);

        return busy is null
            ? null
            : AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeNodeAlreadyInProgress,
                $"Storage node '{busy.Name}' is already being counted in another stocktake.",
                new Dictionary<string, object> { ["nodeId"] = busy.StoragePlaceNodeId });
    }

    public Task<AppProblemDetails?> TransitionAsync(
        Stocktake stocktake, StocktakeTransition transition, CancellationToken ct = default) => transition switch
    {
        StocktakeTransition.Schedule => ScheduleAsync(stocktake, ct),
        StocktakeTransition.ToDraft  => ToDraftAsync(stocktake, ct),
        StocktakeTransition.Start    => StartAsync(stocktake, ct),
        StocktakeTransition.Revert   => RevertAsync(stocktake, ct),
        StocktakeTransition.Finish   => FinishAsync(stocktake, ct),
        StocktakeTransition.Cancel   => CancelAsync(stocktake, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
    };

    private async Task<AppProblemDetails?> ScheduleAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status != StocktakeStatus.Draft)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Draft stocktake can be scheduled.");

        if (stocktake.Type != StocktakeType.Scheduled || stocktake.PlannedDate is null)
            return AppProblems.UnprocessableEntity("plannedDate", ErrorCode.ValidationError,
                "Only a scheduled stocktake with a planned date can be scheduled.");

        if (stocktake.Nodes.Count == 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes,
                "Select at least one storage node before scheduling.");

        stocktake.Status = StocktakeStatus.Planned;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<AppProblemDetails?> ToDraftAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status != StocktakeStatus.Planned)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Planned stocktake can be moved to Draft.");

        stocktake.Status = StocktakeStatus.Draft;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<AppProblemDetails?> StartAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status is not (StocktakeStatus.Draft or StocktakeStatus.Planned))
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a Draft or Planned stocktake can be started.");

        if (stocktake.Nodes.Count == 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes,
                "Select at least one storage node before starting.");

        var conflict = await FindNodeCountedElsewhereAsync(
            stocktake.Id, stocktake.Nodes.Select(n => n.StoragePlaceNodeId).ToList(), ct);
        if (conflict is not null) return conflict;

        stocktake.Status    = StocktakeStatus.InProgress;
        stocktake.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<AppProblemDetails?> RevertAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status != StocktakeStatus.InProgress)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Only a stocktake in progress can be reverted to Draft.");

        // StartedAt is left in place: it records when counting first began, and Start overwrites it
        stocktake.Status = StocktakeStatus.Draft;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<AppProblemDetails?> CancelAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status is StocktakeStatus.Finished or StocktakeStatus.Canceled)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                $"Cannot cancel a stocktake in '{stocktake.Status}' status.");

        stocktake.Status     = StocktakeStatus.Canceled;
        stocktake.CanceledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<AppProblemDetails?> FinishAsync(Stocktake stocktake, CancellationToken ct)
    {
        if (stocktake.Status != StocktakeStatus.InProgress)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeInvalidStatusTransition,
                "Stocktake must be in progress to finish.");

        if (stocktake.Nodes.Count == 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeHasNoNodes, "Stocktake has no nodes.");

        AppProblemDetails? blocked = null;

        try
        {
            await db.Database.ExecuteInTransactionAsync("stocktakes.finish", async () =>
            {
                // Reload inside the transaction so the status check sees the committed state
                var fresh = await WithDetails(db.Stocktakes, includeItems: true).FirstAsync(s => s.Id == stocktake.Id, ct);
                if (fresh.Status != StocktakeStatus.InProgress)
                    return; // finished concurrently

                var plan = await diffCalculator.BuildPlanAsync(fresh, ct);
                if (plan.Problems.Count > 0)
                {
                    var problem = plan.Problems[0];
                    blocked = AppProblems.UnprocessableEntity("root", problem.Code, problem.Message);
                    return;
                }

                await ApplyPlanAsync(plan, fresh, ct);

                fresh.Status     = StocktakeStatus.Finished;
                fresh.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (InventoryWriteConflictException)
        {
            return AppProblems.Conflict(ErrorCode.InventoryWriteConflict,
                "Stock for this item was changed concurrently; nothing was written.");
        }
        catch (InsufficientInventoryException ex)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.InsufficientInventory,
                $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.",
                ex.ToArgs());
        }
        catch (InventoryItemNodeMismatchException)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.StocktakeConcurrentModification,
                "Stock changed while the stocktake was being finished. Refresh and try again.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                "One or more unit items were not found.");
        }
        catch (StoragePlaceNodeNotFoundException)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.StoragePlaceNodeNotFound, "Storage node not found.");
        }
        catch (ValidationException ex)
        {
            return AppProblems.UnprocessableEntity(ex.Field, ex.ErrorCode, ex.Message);
        }

        return blocked;
    }

    /// <summary>
    /// Applies the plan in a fixed order: relocations first so a serial moved between two counted cells
    /// is not detached by the cell that lost it, then detaches, then arrivals, then standard counts.
    /// </summary>
    private async Task ApplyPlanAsync(StocktakePlan plan, Stocktake fresh, CancellationToken ct)
    {
        var itemById = fresh.Nodes.SelectMany(n => n.Items).ToDictionary(i => i.Id);
        var scopeByNodeId = fresh.Nodes.ToDictionary(n => n.StoragePlaceNodeId);
        var context = new StockMovementContext(StocktakeId: fresh.Id);

        foreach (var line in Ordered(plan.Lines))
        {
            switch (line.Resolution)
            {
                case StocktakeDifferenceResolution.Relocation:
                    await inventory.MoveUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeRelocation, context: context, ct: ct);
                    break;

                case StocktakeDifferenceResolution.DetachUnit:
                    await inventory.DetachUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeShortage, context: context, ct: ct);
                    break;

                case StocktakeDifferenceResolution.ReattachUnit:
                    await inventory.ReattachUnitItemAsync(
                        line.UnitInventoryItemId!.Value, line.StoragePlaceNodeId,
                        action: InventoryActions.StocktakeSurplus, context: context, ct: ct);
                    break;

                case StocktakeDifferenceResolution.CreateUnit:
                    try
                    {
                        await inventory.PlaceUnitItemToNodeAsync(
                            line.StoragePlaceNodeId, line.CatalogItemId, line.InventoryNumber!,
                            action: InventoryActions.StocktakeSurplus, context: context, ct: ct);
                    }
                    catch (DbUpdateException e) when (UniqueViolations.IsUnitInventoryNumber(e))
                    {
                        // Race: the soft check passed but the unique index on the number fired
                        throw new ValidationException("inventoryNumber",
                            ErrorCode.UnitInventoryItemNumberDuplicate,
                            $"Инвентарный номер «{line.InventoryNumber}» уже используется для этого товара.");
                    }
                    break;

                case StocktakeDifferenceResolution.Surplus:
                    await inventory.AddStandardItemsToNodeAsync(
                        line.StoragePlaceNodeId, line.CatalogItemId, line.Delta,
                        action: InventoryActions.StocktakeSurplus, context: context, ct: ct);
                    break;

                case StocktakeDifferenceResolution.Shortage:
                    await inventory.RemoveStandardItemsFromNodeAsync(
                        line.StoragePlaceNodeId, line.CatalogItemId, -line.Delta,
                        action: InventoryActions.StocktakeShortage, context: context, ct: ct);
                    break;
            }

            if (line.StocktakeItemId is { } itemId)
            {
                if (itemById.TryGetValue(itemId, out var item))
                    item.AppliedDelta = line.Delta;
            }
            else if (line.Resolution != StocktakeDifferenceResolution.NoChange)
            {
                // Stock the document never mentioned still got written off — persist it as a line so
                // the finished document shows the whole correction, not just the movement journal
                MaterializeLine(line, scopeByNodeId);
            }
        }
    }

    private void MaterializeLine(StocktakePlanLine line, Dictionary<Guid, StocktakeNode> scopeByNodeId)
    {
        if (!scopeByNodeId.TryGetValue(line.StoragePlaceNodeId, out var scopeNode)) return;

        db.StocktakeItems.Add(new StocktakeItem
        {
            Id                  = Guid.NewGuid(),
            StocktakeNodeId     = scopeNode.Id,
            Kind                = line.Kind,
            CatalogItemId       = line.CatalogItemId,
            CountedQuantity     = line.Counted,
            InventoryNumber     = line.InventoryNumber,
            UnitInventoryItemId = line.UnitInventoryItemId,
            Notes               = "Не указано в документе — обнулено при проведении",
            AppliedDelta        = line.Delta,
        });
    }

    private static IEnumerable<StocktakePlanLine> Ordered(IReadOnlyList<StocktakePlanLine> lines)
    {
        static int Rank(StocktakeDifferenceResolution r) => r switch
        {
            StocktakeDifferenceResolution.Relocation => 0,
            StocktakeDifferenceResolution.DetachUnit => 1,
            _ => 2,
        };

        return lines.OrderBy(l => Rank(l.Resolution));
    }
}
