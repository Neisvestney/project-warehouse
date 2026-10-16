using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Writeoffs;

namespace ProjectWarehouse.Server.Services;

public class WriteoffService(ApplicationDbContext db, IInventoryService inventory) : IWriteoffService
{
    public IQueryable<Writeoff> WithDetails(IQueryable<Writeoff> source, bool includeItems = false)
    {
        var q = source
            .Include(w => w.Warehouse)
            .Include(w => w.Tags)
            .Include(w => w.Images).ThenInclude(i => i.DataFile)
            .AsSplitQuery();

        if (includeItems)
            q = q
                .Include(w => w.Items)
                .ThenInclude(i => i.SourceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(w => w.Items)
                .ThenInclude(i => i.CatalogItem)
                .Include(w => w.Items)
                .ThenInclude(i => i.UnitInventoryItem)
                .ThenInclude(u => u!.CatalogItem)
                .AsSplitQuery();

        return q;
    }

    public Task<AppProblemDetails?> TransitionAsync(
        Writeoff writeoff, WriteoffTransition transition, CancellationToken ct = default) => transition switch
    {
        WriteoffTransition.Finish => FinishAsync(writeoff, ct),
        WriteoffTransition.Cancel => CancelAsync(writeoff, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
    };

    private async Task<AppProblemDetails?> FinishAsync(Writeoff writeoff, CancellationToken ct)
    {
        if (writeoff.Status != WriteoffStatus.Draft)
            return AppProblems.UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                "Write-off must be in Draft status to finish.");

        if (writeoff.Items.Count == 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.WriteoffHasNoItems,
                "Write-off has no items.");

        try
        {
            await db.Database.ExecuteInTransactionAsync("writeoffs.finish", async () =>
            {
                // Reload inside the transaction so the status check sees the committed state
                var fresh = await WithDetails(db.Writeoffs, includeItems: true).FirstAsync(w => w.Id == writeoff.Id, ct);

                if (fresh.Status != WriteoffStatus.Draft)
                    return; // finished concurrently

                foreach (var item in fresh.Items)
                {
                    if (item.CatalogItemId.HasValue)
                    {
                        await inventory.RemoveStandardItemsFromNodeAsync(
                            item.SourceNodeId,
                            item.CatalogItemId.Value,
                            item.Count,
                            action: InventoryActions.WrittenOff,
                            context: new StockMovementContext(WriteoffId: fresh.Id),
                            ct: ct);
                    }
                    else if (item.UnitInventoryItemId.HasValue)
                    {
                        await inventory.RemoveUnitItemAsync(
                            item.UnitInventoryItemId.Value,
                            item.SourceNodeId,
                            action: InventoryActions.WrittenOff,
                            context: new StockMovementContext(WriteoffId: fresh.Id),
                            ct: ct);
                    }
                }

                fresh.Status     = WriteoffStatus.Finished;
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
            return AppProblems.UnprocessableEntity("root", ErrorCode.WriteoffInsufficientInventory,
                $"Insufficient inventory at node '{ex.NodeId}': requested {ex.Requested}, available {ex.Available}.",
                ex.ToArgs());
        }
        catch (InventoryItemNodeMismatchException)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.InventoryItemNodeMismatch,
                "One or more items are no longer at the expected storage node.");
        }
        catch (UnitInventoryItemNotFoundException)
        {
            return AppProblems.UnprocessableEntity("root", ErrorCode.UnitInventoryItemNotFound,
                "One or more unit items were not found.");
        }

        return null;
    }

    private async Task<AppProblemDetails?> CancelAsync(Writeoff writeoff, CancellationToken ct)
    {
        if (writeoff.Status is WriteoffStatus.Finished or WriteoffStatus.Canceled)
            return AppProblems.UnprocessableEntity("root", ErrorCode.WriteoffNotDraft,
                $"Cannot cancel a write-off in '{writeoff.Status}' status.");

        writeoff.Status     = WriteoffStatus.Canceled;
        writeoff.CanceledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }
}
