using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Files;
using ProjectWarehouse.Server.Infrastructure.Labels;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public class MarketplaceLabelService(
    ApplicationDbContext db,
    IMarketplaceProviderRegistry providers,
    IMarketplaceCredentialProtector protector,
    IFileStorage storage,
    IDataFileFactory dataFiles,
    LabelPdfComposer composer,
    IOptions<MarketplacesOptions> options,
    ILogger<MarketplaceLabelService> logger) : IMarketplaceLabelService
{
    private readonly MarketplacesOptions _options = options.Value;

    public async Task<LabelBundle> BuildAsync(IReadOnlyList<Guid> orderIds, OrderLabelsGrouping grouping,
        Guid? userId, CancellationToken ct)
    {
        var orders = await db.Orders
            .Where(o => orderIds.Contains(o.Id))
            .Include(o => o.MarketplaceOrder)
            .Include(o => o.MarketplaceItems).ThenInclude(i => i.MarketplaceCard).ThenInclude(c => c!.CatalogItem)
            .ToDictionaryAsync(o => o.Id, ct);

        var nonMarketplace = orderIds.Where(id => !orders.TryGetValue(id, out var o) || o.MarketplaceOrder is null)
            .ToList();
        if (nonMarketplace.Count > 0)
            return new LabelBundle(null, [], nonMarketplace, []);

        // The marketplace only prints labels for awaiting_deliver, so anything else has to already be
        // cached — a stored label reprints at any status, its posting having been packed long ago.
        var notAwaitingDeliver = orderIds
            .Select(id => orders[id].MarketplaceOrder!)
            .Where(mo => mo.LabelFileId is null && mo.Status != MarketplaceOrderStatus.AwaitingDeliver)
            .Select(mo => mo.PostingNumber)
            .ToList();
        if (notAwaitingDeliver.Count > 0)
            return new LabelBundle(null, [], [], notAwaitingDeliver);

        var notReady = new List<string>();
        var documents = new Dictionary<Guid, byte[]>();

        foreach (var group in orders.Values.GroupBy(o => o.MarketplaceOrder!.MarketplaceAccountId))
            await BuildForAccountAsync(group.Key, [.. group], documents, notReady, userId, ct);

        if (notReady.Count > 0)
            return new LabelBundle(null, notReady, [], []);

        var order = OrderPages(orderIds, orders, grouping);
        var merged = LabelPdfComposer.Merge([.. order.Select(id => documents[id])]);
        return new LabelBundle(merged, [], [], []);
    }

    /// <summary>
    /// Default is the caller's order, so the printed stack matches the list on screen. Grouped by article,
    /// orders with an identical set of articles print back to back — the packer takes one pile of identical
    /// goods and works through it instead of walking the shelves per label.
    /// </summary>
    private static IReadOnlyList<Guid> OrderPages(IReadOnlyList<Guid> orderIds,
        IReadOnlyDictionary<Guid, Order> orders, OrderLabelsGrouping grouping)
    {
        if (grouping != OrderLabelsGrouping.Article)
            return orderIds;

        // OrderBy is stable, so inside a group the caller's order survives
        return [.. orderIds.OrderBy(id => ArticleKey(orders[id]), StringComparer.Ordinal)];
    }

    private static string ArticleKey(Order order) =>
        string.Join('\n', BuildArticles(order)
            .Select(a => $"{a.Article} {a.Quantity}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private async Task BuildForAccountAsync(Guid accountId, IReadOnlyList<Order> orders,
        Dictionary<Guid, byte[]> documents, List<string> notReady, Guid? userId, CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new ValidationException("orderIds", ErrorCode.MarketplaceAccountNotFound,
                "The marketplace account of one of the orders no longer exists.");

        var byPostingNumber = orders.ToDictionary(o => o.MarketplaceOrder!.PostingNumber);
        var missing = new List<Order>();

        foreach (var order in orders)
        {
            // already printed once — never regenerate, the label is on a box by now
            if (order.MarketplaceOrder!.LabelFileId is { } fileId)
                documents[order.Id] = await ReadCachedAsync(fileId, ct);
            else
                missing.Add(order);
        }

        if (missing.Count == 0)
            return;

        var provider = providers.Get(account.Type);
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Labels))
            throw new ValidationException("orderIds", ErrorCode.MarketplaceOrdersNotSupported,
                "This marketplace provider cannot produce labels.");

        if (!protector.TryUnprotect(account.ApiKeyProtected, out var apiKey))
            throw new ValidationException("orderIds", ErrorCode.MarketplaceCredentialsUnreadable,
                "The stored API key can no longer be decrypted.");

        var credentials = new MarketplaceCredentials(account.ExternalClientId, apiKey);

        // Multibox postings go one at a time: Ozon does not document whether such a posting prints as
        // one page or one per box, and an unexpected page count would shift a whole batch — putting one
        // order's articles onto another order's box.
        foreach (var order in missing.Where(o => o.MarketplaceOrder!.MultiBoxQty > 1))
            await FetchChunkAsync(provider, credentials, [order.MarketplaceOrder!.PostingNumber],
                byPostingNumber, documents, notReady, userId, allowSplit: false, ct);

        var single = missing.Where(o => o.MarketplaceOrder!.MultiBoxQty <= 1)
            .Select(o => o.MarketplaceOrder!.PostingNumber)
            .ToList();

        foreach (var chunk in single.Chunk(Math.Max(1, _options.Ozon.LabelBatchSize)))
            await FetchChunkAsync(provider, credentials, chunk, byPostingNumber, documents, notReady,
                userId, allowSplit: true, ct);
    }

    /// <summary>
    /// Fetches one chunk. <c>allowSplit</c> is true for batches, where one page is expected per posting;
    /// false for a single-posting request, where the whole response — however many pages — is its label.
    /// </summary>
    private async Task FetchChunkAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        IReadOnlyList<string> chunk, IReadOnlyDictionary<string, Order> byPostingNumber,
        Dictionary<Guid, byte[]> documents, List<string> notReady, Guid? userId, bool allowSplit,
        CancellationToken ct)
    {
        ExternalLabelDocument document;
        try
        {
            document = await provider.FetchLabelDocumentAsync(credentials, chunk, ct);
        }
        catch (MarketplaceApiException) when (allowSplit && chunk.Count > 1)
        {
            await RetryIndividuallyAsync(provider, credentials, chunk, byPostingNumber, documents,
                notReady, userId, ct);
            return;
        }

        if (!document.IsReady || document.Content is null || document.Content.Length == 0)
        {
            // Ozon answers a batch all-or-nothing, so one unready posting hides nineteen ready ones
            if (allowSplit && chunk.Count > 1)
                await RetryIndividuallyAsync(provider, credentials, chunk, byPostingNumber, documents,
                    notReady, userId, ct);
            else
                await MarkNotReadyAsync(chunk, byPostingNumber, notReady, ct);
            return;
        }

        IReadOnlyList<byte[]> perPosting;
        if (allowSplit && chunk.Count > 1)
        {
            var pageCount = LabelPdfComposer.PageCount(document.Content);
            if (pageCount != chunk.Count)
            {
                // The page-per-posting assumption is the only thing mapping pages to postings. If it does
                // not hold, retrying one at a time costs HTTP calls; guessing costs mislabelled boxes.
                logger.LogWarning(
                    "Ozon returned {PageCount} label page(s) for {PostingCount} posting(s); refetching individually",
                    pageCount, chunk.Count);
                await RetryIndividuallyAsync(provider, credentials, chunk, byPostingNumber, documents,
                    notReady, userId, ct);
                return;
            }

            perPosting = LabelPdfComposer.SplitPages(document.Content);
        }
        else
        {
            perPosting = [document.Content];
        }

        for (var i = 0; i < chunk.Count; i++)
            await StoreAsync(byPostingNumber[chunk[i]], perPosting[i],
                document.ContentType ?? "application/pdf", documents, userId, ct);
    }

    private async Task RetryIndividuallyAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        IReadOnlyList<string> chunk, IReadOnlyDictionary<string, Order> byPostingNumber,
        Dictionary<Guid, byte[]> documents, List<string> notReady, Guid? userId, CancellationToken ct)
    {
        foreach (var postingNumber in chunk)
            await FetchChunkAsync(provider, credentials, [postingNumber], byPostingNumber, documents,
                notReady, userId, allowSplit: false, ct);
    }

    private async Task MarkNotReadyAsync(IReadOnlyList<string> postingNumbers,
        IReadOnlyDictionary<string, Order> byPostingNumber, List<string> notReady, CancellationToken ct)
    {
        foreach (var postingNumber in postingNumbers)
        {
            notReady.Add(postingNumber);
            byPostingNumber[postingNumber].MarketplaceOrder!.LabelError = AppProblems.MakeError(
                ErrorCode.MarketplaceLabelNotReady,
                "The marketplace has not produced this label yet.");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task StoreAsync(Order order, byte[] pdf, string contentType,
        Dictionary<Guid, byte[]> documents, Guid? userId, CancellationToken ct)
    {
        var marketplaceOrder = order.MarketplaceOrder!;
        var stamped = composer.Overlay(pdf, BuildArticles(order));

        // The DataFile row commits before LabelFileId is set. A crash in between leaves an orphan that
        // the file GC reclaims after OrphanTtlHours — self-healing, so no transaction is needed.
        using var content = new MemoryStream(stamped);
        var file = await dataFiles.CreateAsync(content, contentType,
            $"label-{marketplaceOrder.PostingNumber}.pdf", stamped.Length, userId, ct: ct);

        marketplaceOrder.LabelFileId = file.Id;
        marketplaceOrder.LabelFetchedAt = DateTime.UtcNow;
        marketplaceOrder.LabelError = null;
        await db.SaveChangesAsync(ct);

        documents[order.Id] = stamped;
    }

    /// <summary>
    /// A snapshot: the articles as mapped at print time. Remapping a card later does not regenerate a
    /// stored label — it is already glued to a box, and a silent rewrite would be worse than the drift.
    /// </summary>
    private static IReadOnlyList<LabelArticle> BuildArticles(Order order) =>
    [
        .. order.MarketplaceItems
            .Where(i => i.MarketplaceCard?.CatalogItem is not null)
            .Select(i => new LabelArticle(i.MarketplaceCard!.CatalogItem!.Article, i.Quantity)),
    ];

    private async Task<byte[]> ReadCachedAsync(Guid fileId, CancellationToken ct)
    {
        var storageKey = await db.DataFiles.Where(f => f.Id == fileId).Select(f => f.StorageKey)
            .FirstAsync(ct);

        await using var stream = await storage.OpenReadAsync(storageKey, ct)
            ?? throw new ValidationException("orderIds", ErrorCode.DataFileNotFound,
                "A cached label file is missing from storage.");

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
