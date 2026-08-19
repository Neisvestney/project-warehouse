using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Services;

public class MarketplaceSyncService(
    ApplicationDbContext db,
    NpgsqlDataSource dataSource,
    IMarketplaceProviderRegistry providers,
    IMarketplaceCredentialProtector protector,
    IChangeLogService<MarketplaceAccountDto> changeLog,
    IMarketplaceOrderSyncService orderSync,
    IRealtimeNotifier realtime,
    IMapper mapper,
    ILogger<MarketplaceSyncService> logger) : IMarketplaceSyncService
{
    public async Task RunAsync(MarketplaceSyncRequest request, CancellationToken ct)
    {
        var run = await db.MarketplaceSyncRuns.FirstOrDefaultAsync(r => r.Id == request.SyncRunId, ct);
        if (run is null)
        {
            logger.LogWarning("Sync run {SyncRunId} vanished before it started", request.SyncRunId);
            return;
        }

        var account = await db.MarketplaceAccounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct);
        if (account is null)
        {
            await FailAsync(run, null, ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.", ct,
                new Dictionary<string, object> { ["accountId"] = request.AccountId });
            return;
        }

        await using var advisoryLock = await PostgresAdvisoryLock.TryAcquireAsync(dataSource, account.Id, ct);
        if (advisoryLock is null)
        {
            await FailAsync(run, account, ErrorCode.MarketplaceSyncAlreadyRunning,
                "A sync is already running for this account.", ct);
            return;
        }

        if (!protector.TryUnprotect(account.ApiKeyProtected, out var apiKey))
        {
            await FailAsync(run, account, ErrorCode.MarketplaceCredentialsUnreadable,
                "The stored API key can no longer be decrypted.", ct);
            return;
        }

        var provider = providers.Get(account.Type);
        var credentials = new MarketplaceCredentials(account.ExternalClientId, apiKey);
        var before = mapper.Map<MarketplaceAccountDto>(account);

        try
        {
            // Scope-independent: one cheap call, and it is what gives the account its name.
            if (provider.Capabilities.HasFlag(MarketplaceCapabilities.SellerInfo))
                await SyncSellerInfoAsync(provider, credentials, account, ct);

            if (run.Scope is MarketplaceSyncScope.Warehouses or MarketplaceSyncScope.All)
                await SyncWarehousesAsync(provider, credentials, account, run, ct);

            if (run.Scope is MarketplaceSyncScope.Cards or MarketplaceSyncScope.All)
                await SyncCardsAsync(provider, credentials, account, run, ct);

            // Orders are outside All on purpose — they only ever run from an explicit user action
            if (run.Scope is MarketplaceSyncScope.Orders)
            {
                if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Orders))
                    throw new ValidationException("accountId", ErrorCode.MarketplaceOrdersNotSupported,
                        "This marketplace provider does not support order sync.");

                await orderSync.SyncOrdersAsync(provider, credentials, account, run, ct);
            }

            run.Status = MarketplaceSyncStatus.Success;
            run.Error = null;
            run.FinishedAt = DateTime.UtcNow;

            account.LastSyncAt = run.FinishedAt;
            account.LastSyncStatus = MarketplaceSyncStatus.Success;
            account.LastSyncError = null;
            await db.SaveChangesAsync(ct);

            await LogFinishedAsync(before, account, run);
            await realtime.PublishFinishedAsync(run, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ValidationException ex)
        {
            // without this arm the generic handler below would relabel it as marketplaceApiError
            await FailAsync(run, account, ex.ErrorCode, ex.Message, ct);
            await LogFinishedAsync(before, account, run);
        }
        catch (MarketplaceApiException ex)
        {
            var code = ex.IsCredentialsRejected
                ? ErrorCode.MarketplaceCredentialsInvalid
                : ErrorCode.MarketplaceApiError;
            await FailAsync(run, account, code, ex.Message, ct, ex.Args);
            await LogFinishedAsync(before, account, run);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Marketplace sync {SyncRunId} failed", run.Id);
            await FailAsync(run, account, ErrorCode.MarketplaceApiError, ex.Message, ct);
            await LogFinishedAsync(before, account, run);
        }
    }

    /// <summary>
    /// Only the run's outcome reaches the changelog — logging every synced card would drown the journal.
    /// Scheduled runs have no user in scope, so UserId stays null, which the schema allows.
    /// </summary>
    private Task LogFinishedAsync(MarketplaceAccountDto before, MarketplaceAccount account, MarketplaceSyncRun run) =>
        changeLog.CompareAndSaveToChangelog(before, mapper.Map<MarketplaceAccountDto>(account),
            MarketplaceActions.SyncFinished,
            new
            {
                syncRunId = run.Id,
                scope = run.Scope,
                status = run.Status,
                cardsCreated = run.CardsCreated,
                cardsArchived = run.CardsArchived,
                autoMapped = run.AutoMapped,
                ordersCreated = run.OrdersCreated,
                ordersUpdated = run.OrdersUpdated,
                ordersSkipped = run.OrdersSkipped,
            });

    /// <summary>
    /// The account name is the marketplace's, not the operator's — nobody types it in. A blank name from the
    /// marketplace leaves the placeholder alone rather than blanking the account out of every list.
    /// </summary>
    private async Task SyncSellerInfoAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, CancellationToken ct)
    {
        var info = await provider.FetchSellerInfoAsync(credentials, ct);

        if (!string.IsNullOrWhiteSpace(info.Name))
            account.Name = info.Name;

        account.CompanyLegalName = info.LegalName;
        account.Inn = info.Inn;
        account.Ogrn = info.Ogrn;
        account.OwnershipForm = info.OwnershipForm;

        await db.SaveChangesAsync(ct);
    }

    private async Task SyncWarehousesAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        var external = await provider.FetchWarehousesAsync(credentials, ct);
        var existing = await db.MarketplaceWarehouses
            .Where(w => w.MarketplaceAccountId == account.Id)
            .ToDictionaryAsync(w => w.ExternalId, ct);

        var now = DateTime.UtcNow;
        var seen = new HashSet<string>();

        foreach (var item in external)
        {
            seen.Add(item.ExternalId);

            if (!existing.TryGetValue(item.ExternalId, out var warehouse))
            {
                warehouse = new MarketplaceWarehouse
                {
                    Id = Guid.NewGuid(),
                    MarketplaceAccountId = account.Id,
                    ExternalId = item.ExternalId,
                };
                db.MarketplaceWarehouses.Add(warehouse);
            }

            warehouse.Name = item.Name;
            warehouse.Kind = item.Kind;
            warehouse.Status = item.Status;
            warehouse.ExternalStatus = item.RawStatus;
            warehouse.Address = item.Address;
            warehouse.IsArchived = false;
            warehouse.SyncedAt = now;
            // WarehouseId is deliberately untouched — the mapping is an administrator's decision
        }

        foreach (var (externalId, warehouse) in existing)
            if (!seen.Contains(externalId))
                warehouse.IsArchived = true;

        run.WarehousesProcessed = external.Count;
        await db.SaveChangesAsync(ct);
        await realtime.PublishProgressAsync(run, ct);
    }

    private async Task SyncCardsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        await foreach (var page in provider.FetchCardsAsync(credentials, ct))
        {
            var externalIds = page.Select(c => c.ExternalId).ToList();
            var existing = await db.MarketplaceCards
                .Where(c => c.MarketplaceAccountId == account.Id && externalIds.Contains(c.ExternalId))
                .ToDictionaryAsync(c => c.ExternalId, ct);

            var now = DateTime.UtcNow;
            var fresh = new List<MarketplaceCard>();

            foreach (var item in page)
            {
                if (!existing.TryGetValue(item.ExternalId, out var card))
                {
                    card = new MarketplaceCard
                    {
                        Id = Guid.NewGuid(),
                        MarketplaceAccountId = account.Id,
                        ExternalId = item.ExternalId,
                    };
                    db.MarketplaceCards.Add(card);
                    fresh.Add(card);
                    run.CardsCreated++;
                }
                else
                {
                    run.CardsUpdated++;
                }

                card.Sku = item.Sku;
                card.OfferId = item.OfferId;
                card.Name = item.Name;
                card.Barcodes = [.. item.Barcodes];
                card.PrimaryImageUrl = item.ImageUrl;
                card.Price = item.Price;
                card.CurrencyCode = item.Currency;
                card.IsArchived = item.IsArchived;
                card.SyncedAt = now;
                // CatalogItemId / MappingSource survive updates — the mapping is independent of card data
            }

            run.CardsProcessed += page.Count;
            await db.SaveChangesAsync(ct);

            run.AutoMapped += await AutoMapAsync(fresh, ct);
            await db.SaveChangesAsync(ct);

            await realtime.PublishProgressAsync(run, ct);
        }

        // Only after a clean pass: a partial run would archive half the catalog.
        run.CardsArchived = await db.MarketplaceCards
            .Where(c => c.MarketplaceAccountId == account.Id && !c.IsArchived && c.SyncedAt < run.StartedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsArchived, true), ct);
    }

    public async Task<int> AutoMapAccountAsync(Guid accountId, CancellationToken ct)
    {
        var unmapped = await db.MarketplaceCards
            .Where(c => c.MarketplaceAccountId == accountId && c.CatalogItemId == null && !c.IsArchived)
            .ToListAsync(ct);

        var mapped = await AutoMapAsync(unmapped, ct);
        await db.SaveChangesAsync(ct);
        return mapped;
    }

    /// <summary>
    /// Maps by seller article first, then by barcode. An existing mapping is never overwritten, and
    /// anything ambiguous (zero or more than one candidate) is left for a human.
    /// </summary>
    private async Task<int> AutoMapAsync(IReadOnlyCollection<MarketplaceCard> cards, CancellationToken ct)
    {
        var candidates = cards.Where(c => c.CatalogItemId is null).ToList();
        if (candidates.Count == 0)
            return 0;

        var offerIds = candidates
            .Select(c => c.OfferId.ToLowerInvariant())
            .Where(o => o.Length > 0)
            .Distinct()
            .ToList();

        var byArticle = await db.CatalogItems
            .Where(i => !i.IsArchived && MarketplaceMapping.MappableTypes.Contains(i.Type) && offerIds.Contains(i.Article.ToLower()))
            .Select(i => new { i.Id, i.Article })
            .GroupBy(i => i.Article.ToLower())
            .ToDictionaryAsync(g => g.Key, g => g.Select(i => i.Id).ToList(), ct);

        var barcodes = candidates
            .SelectMany(c => c.Barcodes)
            .Select(b => b.ToLowerInvariant())
            .Where(b => b.Length > 0)
            .Distinct()
            .ToList();

        var byBarcode = barcodes.Count == 0
            ? []
            : await db.CatalogItems
                .Where(i => !i.IsArchived && MarketplaceMapping.BarcodeMatchableTypes.Contains(i.Type)
                            && i.Barcode != null && barcodes.Contains(i.Barcode.ToLower()))
                .Select(i => new { i.Id, Barcode = i.Barcode! })
                .GroupBy(i => i.Barcode.ToLower())
                .ToDictionaryAsync(g => g.Key, g => g.Select(i => i.Id).ToList(), ct);

        var now = DateTime.UtcNow;
        var mapped = 0;

        foreach (var card in candidates)
        {
            if (TryResolveSingle(byArticle, [card.OfferId.ToLowerInvariant()], out var catalogItemId))
            {
                Apply(card, catalogItemId, MarketplaceMappingSource.AutoOfferId);
                mapped++;
            }
            else if (TryResolveSingle(byBarcode, card.Barcodes.Select(b => b.ToLowerInvariant()), out catalogItemId))
            {
                Apply(card, catalogItemId, MarketplaceMappingSource.AutoBarcode);
                mapped++;
            }
        }

        return mapped;

        void Apply(MarketplaceCard card, Guid catalogItemId, MarketplaceMappingSource source)
        {
            card.CatalogItemId = catalogItemId;
            card.MappingSource = source;
            card.MappedAt = now;
        }
    }

    private static bool TryResolveSingle(Dictionary<string, List<Guid>> index, IEnumerable<string> keys, out Guid id)
    {
        var matches = keys
            .Where(index.ContainsKey)
            .SelectMany(k => index[k])
            .Distinct()
            .Take(2)
            .ToList();

        id = matches.Count == 1 ? matches[0] : Guid.Empty;
        return matches.Count == 1;
    }

    private async Task FailAsync(MarketplaceSyncRun run, MarketplaceAccount? account, ErrorCode code, string message,
        CancellationToken ct, IReadOnlyDictionary<string, object>? args = null)
    {
        var error = AppProblems.MakeError(code, message, args);

        run.Status = MarketplaceSyncStatus.Failed;
        run.Error = error;
        run.FinishedAt = DateTime.UtcNow;

        if (account is not null)
        {
            account.LastSyncAt = run.FinishedAt;
            account.LastSyncStatus = MarketplaceSyncStatus.Failed;
            account.LastSyncError = error;
        }

        await db.SaveChangesAsync(ct);

        // Every failure arm funnels through here, so this is the only place the failed event needs to be raised.
        await realtime.PublishFinishedAsync(run, ct);
    }
}
