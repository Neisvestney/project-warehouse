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

    private const string UnreadableLabelMessage =
        "The label format has changed: the posting barcode cannot be read off the page, so a label cannot "
        + "be matched to its posting. Printing is held back until the reader is updated.";

    public async Task<LabelBundle> BuildAsync(IReadOnlyList<Guid> orderIds, OrderLabelsGrouping grouping,
        Guid? userId, bool forceRegenerate, CancellationToken ct)
    {
        var orders = await db.Orders
            .Where(o => orderIds.Contains(o.Id))
            .Include(o => o.MarketplaceOrder)
            .Include(o => o.MarketplaceItems).ThenInclude(i => i.MarketplaceCard).ThenInclude(c => c!.CatalogItem)
            .ToDictionaryAsync(o => o.Id, ct);

        var nonMarketplace = orderIds.Where(id => !orders.TryGetValue(id, out var o) || o.MarketplaceOrder is null)
            .ToList();
        if (nonMarketplace.Count > 0)
            return new LabelBundle(null, [], nonMarketplace, [], []);

        // The marketplace only prints labels for awaiting_deliver, so anything else has to already be
        // cached — a stored label reprints at any status, its posting having been packed long ago.
        // A forced regenerate cannot lean on that cache, so the status has to hold for every posting.
        var notAwaitingDeliver = orderIds
            .Select(id => orders[id].MarketplaceOrder!)
            .Where(mo => (forceRegenerate || mo.LabelFileId is null)
                         && mo.Status != MarketplaceOrderStatus.AwaitingDeliver)
            .Select(mo => mo.PostingNumber)
            .ToList();
        if (notAwaitingDeliver.Count > 0)
            return new LabelBundle(null, [], [], notAwaitingDeliver, []);

        var failures = new LabelFailures();
        var documents = new Dictionary<Guid, byte[]>();

        foreach (var group in orders.Values.GroupBy(o => o.MarketplaceOrder!.MarketplaceAccountId))
            await BuildForAccountAsync(group.Key, [.. group], documents, failures, userId, forceRegenerate, ct);

        // an unreadable label outranks a missing one: it is a defect to fix, not a wait to sit out
        if (failures.Unreadable.Count > 0)
            return new LabelBundle(null, [], [], [], failures.Unreadable);

        if (failures.NotReady.Count > 0)
            return new LabelBundle(null, failures.NotReady, [], [], []);

        var order = OrderPages(orderIds, orders, grouping);
        var merged = LabelPdfComposer.Merge([.. order.Select(id => documents[id])]);
        return new LabelBundle(merged, [], [], [], []);
    }

    private sealed class LabelFailures
    {
        public List<string> NotReady { get; } = [];

        /// <summary>Held back because the page could not be verified, not because it is missing.</summary>
        public List<string> Unreadable { get; } = [];
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
        Dictionary<Guid, byte[]> documents, LabelFailures failures, Guid? userId, bool forceRegenerate,
        CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new ValidationException("orderIds", ErrorCode.MarketplaceAccountNotFound,
                "The marketplace account of one of the orders no longer exists.");

        var byPostingNumber = orders.ToDictionary(o => o.MarketplaceOrder!.PostingNumber);
        var missing = new List<Order>();

        foreach (var order in orders)
        {
            // already printed once — never regenerate unless asked to, the label is on a box by now
            if (!forceRegenerate && order.MarketplaceOrder!.LabelFileId is { } fileId)
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
                byPostingNumber, documents, failures, userId, allowSplit: false, ct);

        var single = missing.Where(o => o.MarketplaceOrder!.MultiBoxQty <= 1)
            .Select(o => o.MarketplaceOrder!.PostingNumber)
            .ToList();

        foreach (var chunk in single.Chunk(Math.Max(1, _options.Ozon.LabelBatchSize)))
            await FetchChunkAsync(provider, credentials, chunk, byPostingNumber, documents, failures,
                userId, allowSplit: true, ct);
    }

    /// <summary>
    /// Fetches one chunk. <c>allowSplit</c> is true for batches, where one page is expected per posting;
    /// false for a single-posting request, where the whole response — however many pages — is its label.
    /// </summary>
    private async Task FetchChunkAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        IReadOnlyList<string> chunk, IReadOnlyDictionary<string, Order> byPostingNumber,
        Dictionary<Guid, byte[]> documents, LabelFailures failures, Guid? userId, bool allowSplit,
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
                failures, userId, ct);
            return;
        }

        if (!document.IsReady || document.Content is null || document.Content.Length == 0)
        {
            // A verdict on the whole batch with no per-posting detail — the marketplace never started the
            // job, or did not finish it in time. Splitting is the only way to tell a stuck posting apart.
            if (allowSplit && chunk.Count > 1)
                await RetryIndividuallyAsync(provider, credentials, chunk, byPostingNumber, documents,
                    failures, userId, ct);
            else
                await MarkNotReadyAsync(chunk, byPostingNumber, failures, ct);
            return;
        }

        // A named failure is per-posting: the rest of the batch printed and its pages are in hand.
        // Only this chunk's postings count — byPostingNumber spans the whole account.
        var requested = chunk.ToHashSet(StringComparer.Ordinal);
        var unprinted = document.Unprinted
            .Where(u => requested.Contains(u.PostingNumber))
            .ToList();
        if (unprinted.Count > 0)
            await MarkNotReadyAsync(unprinted, byPostingNumber, failures, ct);

        var refused = unprinted.Select(u => u.PostingNumber).ToHashSet(StringComparer.Ordinal);
        var printed = chunk.Where(p => !refused.Contains(p)).ToList();
        if (printed.Count == 0)
            return;

        IReadOnlyList<byte[]> perPosting;
        if (allowSplit && printed.Count > 1)
        {
            var pageCount = LabelPdfComposer.PageCount(document.Content);
            if (pageCount != printed.Count)
            {
                // Page count is the cheap check. If it does not hold, retrying one at a time costs HTTP
                // calls; guessing costs mislabelled boxes.
                logger.LogWarning(
                    "Ozon returned {PageCount} label page(s) for {PostingCount} printed posting(s); refetching individually",
                    pageCount, printed.Count);
                await RetryIndividuallyAsync(provider, credentials, printed, byPostingNumber, documents,
                    failures, userId, ct);
                return;
            }

            var pages = LabelPdfComposer.SplitPages(document.Content);
            switch (MatchByBarcode(printed, byPostingNumber, pages, out var matched))
            {
                case LabelMatch.Matched:
                    perPosting = matched;
                    break;

                case LabelMatch.Unreadable:
                    logger.LogError(
                        "No label page out of {PageCount} could be read; the marketplace has changed the label format",
                        pages.Count);
                    await MarkUnreadableAsync(printed, byPostingNumber, failures, ct);
                    return;

                default:
                    await RetryIndividuallyAsync(provider, credentials, printed, byPostingNumber, documents,
                        failures, userId, ct);
                    return;
            }
        }
        else
        {
            perPosting = [document.Content];
        }

        for (var i = 0; i < printed.Count; i++)
            await StoreAsync(byPostingNumber[printed[i]], perPosting[i],
                document.ContentType ?? "application/pdf", documents, userId, ct);
    }

    /// <summary>
    /// Lays the pages out in <paramref name="chunk"/> order using the barcode printed on each page, so
    /// the mapping does not rest on the marketplace returning pages in the order they were asked for.
    /// </summary>
    /// <remarks>
    /// Postings whose barcode is unknown — imported before the barcode was stored, or never packed —
    /// keep their positional page among the ones no barcode claimed. A barcode that is known but found
    /// on no page, or on a page another posting already claimed, fails the whole chunk: one wrong page
    /// means one wrong box, and refetching individually is the cheap way out.
    /// </remarks>
    private LabelMatch MatchByBarcode(IReadOnlyList<string> chunk,
        IReadOnlyDictionary<string, Order> byPostingNumber, IReadOnlyList<byte[]> pages,
        out IReadOnlyList<byte[]> matched)
    {
        matched = [];

        var barcodes = chunk
            .Select(p => byPostingNumber[p].MarketplaceOrder!.ScanitBarcode)
            .ToList();

        if (barcodes.All(string.IsNullOrWhiteSpace))
        {
            // nothing to match on: the pre-barcode behaviour, page order as requested
            matched = pages;
            return LabelMatch.Matched;
        }

        var pageTexts = pages.Select(LabelTextReader.ReadText).ToList();

        // Not one page gave up its text. That is the reader failing to understand the file rather than a
        // page landing on the wrong posting, and refetching one at a time would only hide it behind a
        // hundred extra calls. Refusing to print is what makes a format change visible on the first batch.
        if (pageTexts.TrueForAll(t => string.IsNullOrWhiteSpace(t)))
            return LabelMatch.Unreadable;

        var result = new byte[chunk.Count][];
        var taken = new bool[pages.Count];

        for (var i = 0; i < chunk.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(barcodes[i]))
                continue;

            var hits = Enumerable.Range(0, pages.Count)
                .Where(p => !taken[p] && LabelTextReader.ContainsBarcode(pageTexts[p], barcodes[i]))
                .ToList();

            if (hits.Count != 1)
            {
                logger.LogWarning(
                    "Label page for {PostingNumber} matched {HitCount} page(s) by barcode; refetching individually",
                    chunk[i], hits.Count);
                return LabelMatch.Mismatch;
            }

            result[i] = pages[hits[0]];
            taken[hits[0]] = true;
        }

        // whatever is left goes to the postings with no barcode, in the order both sides came in
        var spare = new Queue<byte[]>(Enumerable.Range(0, pages.Count).Where(p => !taken[p]).Select(p => pages[p]));
        var unclaimed = result.Count(r => r is null);
        if (unclaimed != spare.Count)
        {
            // cannot happen while pages and postings are equal in number and a match takes exactly one
            // page — but the invariant spans three places, and guessing here costs mislabelled boxes
            logger.LogWarning(
                "{Unclaimed} posting(s) without a barcode page but {Spare} page(s) left; refetching individually",
                unclaimed, spare.Count);
            return LabelMatch.Mismatch;
        }

        for (var i = 0; i < chunk.Count; i++)
            if (result[i] is null)
                result[i] = spare.Dequeue();

        matched = result;
        return LabelMatch.Matched;
    }

    private enum LabelMatch
    {
        Matched,

        /// <summary>A page was read but claimed by no posting, or by more than one.</summary>
        Mismatch,

        /// <summary>No page yielded any text at all — the reader does not understand the file.</summary>
        Unreadable,
    }

    private async Task RetryIndividuallyAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        IReadOnlyList<string> chunk, IReadOnlyDictionary<string, Order> byPostingNumber,
        Dictionary<Guid, byte[]> documents, LabelFailures failures, Guid? userId, CancellationToken ct)
    {
        foreach (var postingNumber in chunk)
            await FetchChunkAsync(provider, credentials, [postingNumber], byPostingNumber, documents,
                failures, userId, allowSplit: false, ct);
    }

    private Task MarkNotReadyAsync(IReadOnlyList<string> postingNumbers,
        IReadOnlyDictionary<string, Order> byPostingNumber, LabelFailures failures, CancellationToken ct) =>
        MarkAsync([.. postingNumbers.Select(p => new ExternalLabelFailure(p, null))],
            ErrorCode.MarketplaceLabelNotReady, byPostingNumber, failures, ct);

    private Task MarkNotReadyAsync(IReadOnlyList<ExternalLabelFailure> unprinted,
        IReadOnlyDictionary<string, Order> byPostingNumber, LabelFailures failures, CancellationToken ct) =>
        MarkAsync(unprinted, ErrorCode.MarketplaceLabelNotReady, byPostingNumber, failures, ct);

    private Task MarkUnreadableAsync(IReadOnlyList<string> postingNumbers,
        IReadOnlyDictionary<string, Order> byPostingNumber, LabelFailures failures, CancellationToken ct) =>
        MarkAsync([.. postingNumbers.Select(p => new ExternalLabelFailure(p, UnreadableLabelMessage))],
            ErrorCode.MarketplaceLabelFormatChanged, byPostingNumber, failures, ct);

    /// <summary>
    /// Records why a label is missing. The marketplace's own wording, when it gave one, goes into
    /// <see cref="MarketplaceOrder.LabelError"/> — it is the only clue the packer gets about a posting
    /// the marketplace refuses to print.
    /// </summary>
    private async Task MarkAsync(IReadOnlyList<ExternalLabelFailure> reasons, ErrorCode code,
        IReadOnlyDictionary<string, Order> byPostingNumber, LabelFailures failures, CancellationToken ct)
    {
        var bucket = code == ErrorCode.MarketplaceLabelFormatChanged ? failures.Unreadable : failures.NotReady;

        foreach (var reason in reasons)
        {
            bucket.Add(reason.PostingNumber);
            byPostingNumber[reason.PostingNumber].MarketplaceOrder!.LabelError = AppProblems.MakeError(code,
                string.IsNullOrWhiteSpace(reason.Message)
                    ? "The marketplace has not produced this label yet."
                    : reason.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task StoreAsync(Order order, byte[] pdf, string contentType,
        Dictionary<Guid, byte[]> documents, Guid? userId, CancellationToken ct)
    {
        var marketplaceOrder = order.MarketplaceOrder!;
        var stamped = composer.Overlay(pdf, BuildArticles(order));

        // The DataFile row commits before LabelFileId is set. A crash in between leaves an orphan that
        // the file GC reclaims after OrphanTtlHours — self-healing, so no transaction is needed. The
        // same GC picks up the file a regenerate replaces.
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
            .Select(i => new LabelArticle(i.MarketplaceCard!.CatalogItem!.EffectiveLabelText, i.Quantity)),
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
