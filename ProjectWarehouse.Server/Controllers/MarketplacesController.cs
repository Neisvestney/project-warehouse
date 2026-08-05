using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Integrations;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/integrations/marketplaces")]
public class MarketplacesController(
    ApplicationDbContext db,
    IMapper mapper,
    IMarketplaceProviderRegistry providers,
    IMarketplaceCredentialProtector protector,
    IMarketplaceSyncQueue syncQueue,
    IMarketplaceSyncService syncService,
    IOptions<MarketplacesOptions> options,
    IChangeLogService<MarketplaceAccountDto> accountChangeLog,
    IChangeLogService<MarketplaceCardDto> cardChangeLog) : AppControllerBase
{
    // ---------- accounts ----------

    /// <summary>List marketplace accounts (paginated, searchable).</summary>
    [HttpGet("accounts")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<Paginated<MarketplaceAccountSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] MarketplaceType? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] MarketplaceAccountSortBy sortBy = MarketplaceAccountSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var query = db.MarketplaceAccounts.WhereMatchesSearch(a => a.SearchString, searchString);

        if (type is not null)
            query = query.Where(a => a.Type == type);
        if (isActive is not null)
            query = query.Where(a => a.IsActive == isActive);

        var sorted = sortBy switch
        {
            MarketplaceAccountSortBy.CreatedAt => query.Sort(a => a.CreatedAt, sortOrder),
            MarketplaceAccountSortBy.LastSyncAt => query.Sort(a => a.LastSyncAt, sortOrder),
            _ => query.Sort(a => a.Name, sortOrder),
        };

        var paginated = await sorted
            .ThenBy(a => a.Id)
            .ProjectTo<MarketplaceAccountSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Account with aggregates. Probes the stored key so the UI can warn about a lost key ring.</summary>
    [HttpGet("accounts/{id:guid}")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<MarketplaceAccountDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccount(Guid id, CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (account is null)
            return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

        return Ok(await ToDetailDtoAsync(account, ct));
    }

    /// <summary>Connects a marketplace account. The key is encrypted on write and never returned.</summary>
    [HttpPost("accounts")]
    [Authorize(Policy = Permissions.Integrations.Edit)]
    [ProducesResponseType<MarketplaceAccountDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateMarketplaceAccountRequest request, CancellationToken ct)
    {
        if (!providers.TryGet(request.Type, out var provider))
            return UnprocessableEntity(nameof(request.Type), ErrorCode.MarketplaceApiError,
                $"Marketplace {request.Type} is not supported yet.");

        if (provider.RequiresClientId && string.IsNullOrWhiteSpace(request.ClientId))
            return UnprocessableEntity(nameof(request.ClientId), ErrorCode.MarketplaceClientIdRequired,
                "This marketplace requires a client id.");

        var now = DateTime.UtcNow;
        var account = new MarketplaceAccount
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            // stands in until the first sync reports the real shop name — the account still has to be listable
            Name = $"{request.Type} ••••{protector.Last4(request.ApiKey)}",
            IsActive = request.IsActive,
            ExternalClientId = request.ClientId,
            ApiKeyProtected = protector.Protect(request.ApiKey),
            ApiKeyLast4 = protector.Last4(request.ApiKey),
            ApiKeyUpdatedAt = now,
            SyncIntervalMinutes = request.SyncIntervalMinutes ?? options.Value.DefaultSyncIntervalMinutes,
            CreatedAt = now,
            CreatedById = GetCurrentUserId(),
        };

        db.MarketplaceAccounts.Add(account);
        await db.SaveChangesAsync(ct);

        var dto = await ToDetailDtoAsync(account, ct);
        await accountChangeLog.CompareAndSaveToChangelog(null, dto, MarketplaceActions.AccountCreated,
            new { marketplace = account.Type });

        if (account.IsActive)
            await EnqueueSyncAsync(account, MarketplaceSyncScope.All, ct);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, dto);
    }

    /// <summary>Updates an account. An empty <c>apiKey</c> keeps the stored one.</summary>
    [HttpPut("accounts/{id:guid}")]
    [Authorize(Policy = Permissions.Integrations.Edit)]
    [ProducesResponseType<MarketplaceAccountDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateMarketplaceAccountRequest request,
        CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (account is null)
            return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

        var provider = providers.Get(account.Type);
        if (provider.RequiresClientId && string.IsNullOrWhiteSpace(request.ClientId))
            return UnprocessableEntity(nameof(request.ClientId), ErrorCode.MarketplaceClientIdRequired,
                "This marketplace requires a client id.");

        var before = await ToDetailDtoAsync(account, ct);

        account.ExternalClientId = request.ClientId;
        account.IsActive = request.IsActive;
        account.SyncIntervalMinutes = request.SyncIntervalMinutes;

        var keyRotated = !string.IsNullOrWhiteSpace(request.ApiKey);
        if (keyRotated)
        {
            account.ApiKeyProtected = protector.Protect(request.ApiKey!);
            account.ApiKeyLast4 = protector.Last4(request.ApiKey!);
            account.ApiKeyUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var after = await ToDetailDtoAsync(account, ct);
        await accountChangeLog.CompareAndSaveToChangelog(before, after,
            keyRotated ? MarketplaceActions.AccountKeyRotated : MarketplaceActions.AccountUpdated,
            new { marketplace = account.Type });

        return Ok(after);
    }

    /// <summary>Disconnects an account, cascading to its synced warehouses, cards and run history.</summary>
    [HttpDelete("accounts/{id:guid}")]
    [Authorize(Policy = Permissions.Integrations.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAccount(Guid id, CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (account is null)
            return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

        var before = await ToDetailDtoAsync(account, ct);

        db.MarketplaceAccounts.Remove(account);
        await db.SaveChangesAsync(ct);

        await accountChangeLog.CompareAndSaveToChangelog(before, null, MarketplaceActions.AccountDeleted,
            new { marketplace = account.Type });

        return NoContent();
    }

    /// <summary>
    /// Checks credentials without saving. When the body carries an apiKey the route id is ignored,
    /// so a key can be verified before the account exists.
    /// </summary>
    [HttpPost("accounts/{id}/test-connection")]
    [Authorize(Policy = Permissions.Integrations.Edit)]
    [ProducesResponseType<TestConnectionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnection(string id, [FromBody] TestConnectionRequest request, CancellationToken ct)
    {
        IMarketplaceProvider provider;
        MarketplaceCredentials credentials;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            var type = request.Type ?? MarketplaceType.Ozon;
            if (!providers.TryGet(type, out provider!))
                return UnprocessableEntity(nameof(request.Type), ErrorCode.MarketplaceApiError,
                    $"Marketplace {type} is not supported yet.");

            if (provider.RequiresClientId && string.IsNullOrWhiteSpace(request.ClientId))
                return UnprocessableEntity(nameof(request.ClientId), ErrorCode.MarketplaceClientIdRequired,
                    "This marketplace requires a client id.");

            credentials = new MarketplaceCredentials(request.ClientId, request.ApiKey);
        }
        else
        {
            if (!Guid.TryParse(id, out var accountId))
                return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

            var account = await db.MarketplaceAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
            if (account is null)
                return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

            if (!protector.TryUnprotect(account.ApiKeyProtected, out var apiKey))
                return UnprocessableEntity("root", ErrorCode.MarketplaceCredentialsUnreadable,
                    "The stored API key can no longer be decrypted — enter it again.");

            provider = providers.Get(account.Type);
            credentials = new MarketplaceCredentials(account.ExternalClientId, apiKey);
        }

        try
        {
            var result = await provider.ValidateAsync(credentials, ct);
            if (!result.IsValid)
                return UnprocessableEntity("apiKey", ErrorCode.MarketplaceCredentialsInvalid,
                    result.Message ?? "The marketplace rejected these credentials.", result.Args);

            return Ok(new TestConnectionResponse { IsValid = true });
        }
        catch (MarketplaceApiException ex)
        {
            return Problem(AppProblems.Root(StatusCodes.Status502BadGateway, ErrorCode.MarketplaceApiError,
                ex.Message, ex.Args));
        }
    }

    // ---------- sync ----------

    /// <summary>Queues a sync and returns 202 immediately — poll the run for progress.</summary>
    [HttpPost("accounts/{id:guid}/sync")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<StartSyncResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartSync(Guid id, [FromBody] StartSyncRequest request, CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (account is null)
            return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

        // cheap UX guard; the worker's advisory lock is what actually guarantees exclusivity
        var alreadyRunning = await db.MarketplaceSyncRuns
            .AnyAsync(r => r.MarketplaceAccountId == id && r.Status == MarketplaceSyncStatus.Running, ct);

        if (alreadyRunning)
            return Conflict(ErrorCode.MarketplaceSyncAlreadyRunning, "A sync is already running for this account.");

        var runId = await EnqueueSyncAsync(account, request.Scope, ct);

        // plain Accepted(obj) would emit a bogus Location header
        return StatusCode(StatusCodes.Status202Accepted, new StartSyncResponse { SyncRunId = runId });
    }

    /// <summary>Sync history for an account, newest first (paginated).</summary>
    [HttpGet("accounts/{id:guid}/sync-runs")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<Paginated<MarketplaceSyncRunDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncRuns(Guid id,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        CancellationToken ct = default)
    {
        var paginated = await db.MarketplaceSyncRuns
            .Where(r => r.MarketplaceAccountId == id)
            .OrderByDescending(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .ProjectTo<MarketplaceSyncRunDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    // ---------- warehouses ----------

    /// <summary>Marketplace warehouses of an account with their WMS mapping (paginated, searchable).</summary>
    [HttpGet("accounts/{id:guid}/warehouses")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<Paginated<MarketplaceWarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouses(Guid id,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 50,
        [FromQuery] bool includeArchived = false,
        [FromQuery] MarketplaceWarehouseSortBy sortBy = MarketplaceWarehouseSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var query = db.MarketplaceWarehouses.Where(w => w.MarketplaceAccountId == id);
        if (!includeArchived)
            query = query.Where(w => !w.IsArchived);

        var sorted = sortBy switch
        {
            MarketplaceWarehouseSortBy.Kind => query.Sort(w => w.Kind, sortOrder),
            MarketplaceWarehouseSortBy.SyncedAt => query.Sort(w => w.SyncedAt, sortOrder),
            _ => query.Sort(w => w.Name, sortOrder),
        };

        var paginated = await sorted
            .ThenBy(w => w.Id)
            .ProjectTo<MarketplaceWarehouseDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Maps a marketplace warehouse to a WMS warehouse. Null clears the mapping.</summary>
    [HttpPut("warehouses/{id:guid}/mapping")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<MarketplaceWarehouseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetWarehouseMapping(Guid id, [FromBody] SetWarehouseMappingRequest request,
        CancellationToken ct)
    {
        var warehouse = await db.MarketplaceWarehouses
            .Include(w => w.Warehouse)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouse is null)
            return NotFound(ErrorCode.MarketplaceWarehouseNotFound, "Marketplace warehouse not found.");

        if (request.WarehouseId is { } warehouseId
            && !await db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct))
            return UnprocessableEntity(nameof(request.WarehouseId), ErrorCode.WarehouseNotFound, "Warehouse not found.");

        warehouse.WarehouseId = request.WarehouseId;
        await db.SaveChangesAsync(ct);

        var dto = await db.MarketplaceWarehouses
            .Where(w => w.Id == id)
            .ProjectTo<MarketplaceWarehouseDto>(mapper.ConfigurationProvider)
            .FirstAsync(ct);

        return Ok(dto);
    }

    // ---------- cards ----------

    /// <summary>Marketplace cards of an account with their catalog mapping (paginated, searchable, filterable).</summary>
    [HttpGet("accounts/{id:guid}/cards")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<Paginated<MarketplaceCardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCards(Guid id,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 50,
        [FromQuery] string? searchString = null,
        [FromQuery] MarketplaceCardMappingState mappingState = MarketplaceCardMappingState.All,
        [FromQuery] bool includeArchived = false,
        [FromQuery] MarketplaceCardSortBy sortBy = MarketplaceCardSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var query = db.MarketplaceCards
            .Where(c => c.MarketplaceAccountId == id)
            .WhereMatchesSearch(c => c.SearchString, searchString);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        query = mappingState switch
        {
            MarketplaceCardMappingState.Unmapped => query.Where(c => c.CatalogItemId == null),
            MarketplaceCardMappingState.Mapped => query.Where(c => c.CatalogItemId != null),
            MarketplaceCardMappingState.ArchivedItem => query.Where(c => c.IsMappedToArchivedItem),
            _ => query,
        };

        var sorted = sortBy switch
        {
            MarketplaceCardSortBy.OfferId => query.Sort(c => c.OfferId, sortOrder),
            MarketplaceCardSortBy.Price => query.Sort(c => c.Price, sortOrder),
            MarketplaceCardSortBy.SyncedAt => query.Sort(c => c.SyncedAt, sortOrder),
            _ => query.Sort(c => c.Name, sortOrder),
        };

        var paginated = await sorted
            .ThenBy(c => c.Id)
            .ProjectTo<MarketplaceCardDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Maps a card to a catalog item. Null clears the mapping.</summary>
    [HttpPut("cards/{id:guid}/mapping")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<MarketplaceCardDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCardMapping(Guid id, [FromBody] SetCardMappingRequest request, CancellationToken ct)
    {
        var card = await db.MarketplaceCards
            .Include(c => c.CatalogItem)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (card is null)
            return NotFound(ErrorCode.MarketplaceCardNotFound, "Marketplace card not found.");

        if (request.CatalogItemId is { } catalogItemId)
        {
            var target = await db.CatalogItems
                .Where(i => i.Id == catalogItemId)
                .Select(i => new { i.Type, i.IsArchived })
                .FirstOrDefaultAsync(ct);

            if (target is null)
                return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.CatalogItemNotFound,
                    "Catalog item not found.");

            if (!MarketplaceMapping.MappableTypes.Contains(target.Type))
                return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.MarketplaceCardMappingTypeNotAllowed,
                    "A product group cannot be an order component and cannot back a marketplace card.");

            // only blocked when setting: an item archived after the fact keeps its mapping
            if (target.IsArchived)
                return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.MarketplaceCardMappingArchivedItem,
                    "The catalog item is archived.");
        }

        var before = await ProjectCardAsync(id, ct);

        card.CatalogItemId = request.CatalogItemId;
        card.MappingSource = request.CatalogItemId is null ? null : MarketplaceMappingSource.Manual;
        card.MappedAt = request.CatalogItemId is null ? null : DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var after = await ProjectCardAsync(id, ct);
        await cardChangeLog.CompareAndSaveToChangelog(before, after,
            request.CatalogItemId is null ? MarketplaceActions.MappingCleared : MarketplaceActions.MappingSet,
            request.CatalogItemId is null ? null : new { catalogItemId = request.CatalogItemId, source = "manual" });

        return Ok(after);
    }

    /// <summary>Matches still-unmapped cards to catalog items by article, then by barcode. Existing mappings are left alone.</summary>
    [HttpPost("accounts/{id:guid}/cards/auto-map")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<AutoMapResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AutoMapCards(Guid id, CancellationToken ct)
    {
        var account = await db.MarketplaceAccounts
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (account is null)
            return NotFound(ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");

        var before = await ToDetailDtoAsync(account, ct);
        var mapped = await syncService.AutoMapAccountAsync(id, ct);

        var remaining = await db.MarketplaceCards
            .CountAsync(c => c.MarketplaceAccountId == id && c.CatalogItemId == null && !c.IsArchived, ct);

        // diffs on unmappedCardCount, so a run that mapped nothing writes no entry
        await accountChangeLog.CompareAndSaveToChangelog(before, await ToDetailDtoAsync(account, ct),
            MarketplaceActions.MappingAuto, new { matched = mapped, remaining });

        return Ok(new AutoMapResponse { Mapped = mapped, Remaining = remaining });
    }

    /// <summary>Unmapped card count across all active accounts — feeds the sidebar badge.</summary>
    [HttpGet("accounts/unmapped-count")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<UnmappedCardsCountDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnmappedCount(CancellationToken ct)
    {
        var count = await db.MarketplaceCards
            .CountAsync(c => c.CatalogItemId == null && !c.IsArchived && c.MarketplaceAccount.IsActive, ct);

        return Ok(new UnmappedCardsCountDto { Count = count });
    }

    // ---------- helpers ----------

    private async Task<Guid> EnqueueSyncAsync(MarketplaceAccount account, MarketplaceSyncScope scope, CancellationToken ct)
    {
        var run = new MarketplaceSyncRun
        {
            Id = Guid.NewGuid(),
            MarketplaceAccountId = account.Id,
            Scope = scope,
            Status = MarketplaceSyncStatus.Running,
            StartedAt = DateTime.UtcNow,
            TriggeredById = GetCurrentUserId(),
        };

        db.MarketplaceSyncRuns.Add(run);
        // committed on its own so the UI sees progress before the run finishes
        await db.SaveChangesAsync(ct);

        await syncQueue.EnqueueAsync(new MarketplaceSyncRequest(account.Id, run.Id, scope), ct);
        return run.Id;
    }

    private async Task<MarketplaceAccountDto> ToDetailDtoAsync(MarketplaceAccount account, CancellationToken ct)
    {
        var dto = await db.MarketplaceAccounts
            .Where(a => a.Id == account.Id)
            .ProjectTo<MarketplaceAccountDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct) ?? mapper.Map<MarketplaceAccountDto>(account);

        dto.CredentialsUnreadable = !protector.TryUnprotect(account.ApiKeyProtected, out _);
        dto.Capabilities = providers.TryGet(account.Type, out var provider)
            ? provider.Capabilities
            : MarketplaceCapabilities.None;

        return dto;
    }

    private Task<MarketplaceCardDto> ProjectCardAsync(Guid id, CancellationToken ct) =>
        db.MarketplaceCards
            .Where(c => c.Id == id)
            .ProjectTo<MarketplaceCardDto>(mapper.ConfigurationProvider)
            .FirstAsync(ct);
}
