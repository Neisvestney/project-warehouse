using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Services;

public class ReceiptService(ApplicationDbContext db) : IReceiptService
{
    public IQueryable<Receipt> WithDetails(IQueryable<Receipt> source, bool includeItems = false)
    {
        var q = source
            .Include(r => r.Warehouse)
            .Include(r => r.Tags)
            .Include(r => r.Images).ThenInclude(i => i.DataFile)
            .AsSplitQuery();

        if (includeItems)
            q = q.Include(r => r.Items)
                .ThenInclude(i => i.CatalogItem).ThenInclude(c => c.Group)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.StoragePlaceNode)
                .ThenInclude(n => n.RootStoragePlace)
                .Include(r => r.Items)
                .ThenInclude(i => i.Placements)
                .ThenInclude(p => p.UnitInventoryItem);

        return q;
    }

    /// <summary>Units one placement stands for: a Unit placement carries no count and always means one.</summary>
    public static int PlacedUnits(ReceiptItemPlacement p) => p.Count == 0 ? 1 : p.Count;

    public static int TotalPlaced(ReceiptItem item) => item.Placements.Sum(PlacedUnits);

    public async Task<AppProblemDetails?> TransitionAsync(
        Receipt receipt, ReceiptTransition transition, CancellationToken ct = default)
    {
        var problem = transition switch
        {
            ReceiptTransition.Plan            => Step(receipt, ReceiptStatus.Draft, ReceiptStatus.Planned),
            ReceiptTransition.StartProcessing => Step(receipt, ReceiptStatus.Planned, ReceiptStatus.Processing),
            ReceiptTransition.Finish          => Finish(receipt),
            ReceiptTransition.Revert          => Revert(receipt),
            ReceiptTransition.Cancel          => Cancel(receipt),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
        };
        if (problem is not null) return problem;

        await db.SaveChangesAsync(ct);
        return null;
    }

    private static AppProblemDetails? Step(Receipt receipt, ReceiptStatus from, ReceiptStatus to)
    {
        if (receipt.Status != from)
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Receipt must be in '{from}' status to perform this action (current: '{receipt.Status}').");

        receipt.Status = to;
        if (to == ReceiptStatus.Processing)
            receipt.StartedAt = DateTime.UtcNow;
        return null;
    }

    private static AppProblemDetails? Finish(Receipt receipt)
    {
        if (receipt.Status != ReceiptStatus.Processing)
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Receipt must be in 'Processing' status to finish (current: '{receipt.Status}').");

        var counted = receipt.Items.Where(i => i.ReceivedCount.HasValue).ToList();

        var underplaced = counted.Where(i => TotalPlaced(i) < i.ReceivedCount!.Value).ToList();
        if (underplaced.Count > 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptItemsUnderplaced,
                $"Некоторые позиции размещены не полностью: {string.Join(", ", underplaced.Select(i => i.CatalogItem?.Name ?? i.Id.ToString()))}.");

        var overplaced = counted.Where(i => TotalPlaced(i) > i.ReceivedCount!.Value).ToList();
        if (overplaced.Count > 0)
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptItemsOverplaced,
                $"Некоторые позиции размещены сверх принятого количества: {string.Join(", ", overplaced.Select(i => i.CatalogItem?.Name ?? i.Id.ToString()))}.");

        receipt.Status     = ReceiptStatus.Finished;
        receipt.FinishedAt = DateTime.UtcNow;
        return null;
    }

    private static AppProblemDetails? Revert(Receipt receipt)
    {
        switch (receipt.Status)
        {
            case ReceiptStatus.Planned:
                receipt.Status = ReceiptStatus.Draft;
                return null;

            case ReceiptStatus.Processing:
                if (receipt.Items.Any(i => i.Placements.Count > 0))
                    return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptHasPlacements,
                        "Cannot revert from Processing: some items already have placements. Remove them first.");
                receipt.Status = ReceiptStatus.Planned;
                return null;

            case ReceiptStatus.Finished:
                receipt.Status     = ReceiptStatus.Processing;
                receipt.FinishedAt = null;
                return null;

            default:
                return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                    $"Cannot revert from '{receipt.Status}' status.");
        }
    }

    private static AppProblemDetails? Cancel(Receipt receipt)
    {
        if (receipt.Status is ReceiptStatus.Finished or ReceiptStatus.Canceled)
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptInvalidStatusTransition,
                $"Cannot cancel a receipt in '{receipt.Status}' status.");

        if (receipt.Status == ReceiptStatus.Processing && receipt.Items.Any(i => i.Placements.Count > 0))
            return AppProblems.UnprocessableEntity("root", ErrorCode.ReceiptHasPlacements,
                "Cannot cancel: some items already have placements. Remove them first.");

        receipt.Status     = ReceiptStatus.Canceled;
        receipt.CanceledAt = DateTime.UtcNow;
        return null;
    }
}
