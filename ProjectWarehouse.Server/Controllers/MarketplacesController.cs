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
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>type</c>, <c>isActive</c>, <c>sortBy</c> (default <c>Name</c>), <c>sortOrder</c> (default <c>Asc</c>).
    /// Requires <c>integrations.view</c>; 403 <c>permissionDenied</c> otherwise.
    /// </remarks>
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

    /// <summary>
    /// Id/name/type only, for filter dropdowns. Open to any authenticated user on purpose: the orders
    /// pages filter by account, and a picker there must not require integrations.view.
    /// </summary>
    /// <remarks>
    /// Query params: <c>type</c> (optional). Authentication is the only requirement, so the sole error this
    /// endpoint can produce is a 401 from the auth layer — <c>tokenInvalid</c> when the <c>sub</c> claim is
    /// unusable, otherwise a bare 401 with no body.
    /// </remarks>
    [HttpGet("accounts/short")]
    [Authorize]
    [ProducesResponseType<List<MarketplaceAccountShortSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountsShort(
        [FromQuery] MarketplaceType? type = null,
        CancellationToken ct = default)
    {
        var query = db.MarketplaceAccounts.AsQueryable();

        if (type is not null)
            query = query.Where(a => a.Type == type);

        var accounts = await query
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Id)
            .ProjectTo<MarketplaceAccountShortSummaryDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(accounts);
    }

    /// <summary>Account with aggregates. Probes the stored key so the UI can warn about a lost key ring.</summary>
    /// <remarks>
    /// An unreadable key is reported as <c>credentialsUnreadable: true</c> on the DTO, not as an error —
    /// the account still has to be viewable and editable so the key can be entered again.
    /// Returns 404 <c>marketplaceAccountNotFound</c>. Requires <c>integrations.view</c>.
    /// </remarks>
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
    /// <remarks>
    /// Body: <c>CreateMarketplaceAccountRequest</c> — <c>type</c>, <c>apiKey</c>, <c>clientId</c>,
    /// <c>isActive</c>, <c>syncIntervalMinutes</c> (defaults to <c>Marketplaces:DefaultSyncIntervalMinutes</c>).
    /// An active account is enqueued for a full sync right away, so its first errors surface on the run, not here.
    /// Errors:
    /// <list type="bullet">
    ///   <item>422 <c>marketplaceApiError</c> on <c>type</c> — no provider is registered for that marketplace</item>
    ///   <item>422 <c>marketplaceClientIdRequired</c> on <c>clientId</c> — the provider declares <c>requiresClientId</c> and none was supplied</item>
    /// </list>
    /// Requires <c>integrations.edit</c>.
    /// </remarks>
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
    /// <remarks>
    /// The account type is fixed at creation and is not part of the request. Errors:
    /// <list type="bullet">
    ///   <item>404 <c>marketplaceAccountNotFound</c></item>
    ///   <item>422 <c>marketplaceClientIdRequired</c> on <c>clientId</c> — the provider declares <c>requiresClientId</c> and none was supplied</item>
    /// </list>
    /// The new key is not verified here — use <c>POST accounts/{id}/test-connection</c> for that.
    /// Requires <c>integrations.edit</c>.
    /// </remarks>
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
    /// <remarks>
    /// Returns 404 <c>marketplaceAccountNotFound</c>, or 409 <c>marketplaceAccountHasOrders</c> when any
    /// posting was imported through it — those orders are warehouse history and outlive the connection.
    /// Requires <c>integrations.edit</c>.
    /// </remarks>
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

        // Without this pre-check the Restrict FK raises a raw 23503 and the client gets an unrenderable 500
        if (await db.MarketplaceOrders.AnyAsync(o => o.MarketplaceAccountId == id, ct))
            return Conflict(ErrorCode.MarketplaceAccountHasOrders,
                "The account has imported orders and cannot be deleted.");

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
    /// <remarks>
    /// Body: <c>TestConnectionRequest</c> — <c>apiKey</c>, <c>clientId</c>, <c>type</c> (default <c>Ozon</c>).
    /// With an <c>apiKey</c> the route id may be any string (the UI passes <c>"new"</c>); without one it must
    /// parse as a GUID of an existing account. Errors:
    /// <list type="bullet">
    ///   <item>422 <c>marketplaceApiError</c> on <c>type</c> — no provider is registered for that marketplace</item>
    ///   <item>422 <c>marketplaceClientIdRequired</c> on <c>clientId</c></item>
    ///   <item>404 <c>marketplaceAccountNotFound</c> — id is unparsable or unknown (only on the stored-key path)</item>
    ///   <item>422 <c>marketplaceCredentialsUnreadable</c> on <c>root</c> — the stored key cannot be decrypted (Data Protection key ring lost)</item>
    ///   <item>422 <c>marketplaceCredentialsInvalid</c> on <c>apiKey</c> — the marketplace rejected the credentials (401/403); <c>args</c>: <c>marketplaceStatus</c>, optional <c>marketplaceResponse</c></item>
    ///   <item>502 <c>marketplaceApiError</c> on <c>root</c> — the marketplace errored or is unreachable; <c>args</c>: <c>marketplaceStatus</c>, optional <c>marketplaceResponse</c></item>
    /// </list>
    /// <c>marketplaceResponse</c> is the marketplace's body truncated to 2000 characters; request headers,
    /// where the key travels, never appear in these errors. Requires <c>integrations.edit</c>.
    /// </remarks>
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
    /// <remarks>
    /// Body: <c>StartSyncRequest</c> — <c>scope</c> (<c>All</c>, <c>Warehouses</c>, <c>Cards</c>, <c>Orders</c>).
    /// Answers 202 with <c>StartSyncResponse.syncRunId</c>; poll it through <c>GET sync-runs?ids=</c>.
    /// Errors returned by this call:
    /// <list type="bullet">
    ///   <item>404 <c>marketplaceAccountNotFound</c></item>
    ///   <item>409 <c>marketplaceSyncAlreadyRunning</c> — a run for this account is still <c>Running</c></item>
    /// </list>
    /// A rejection can arrive two ways and a client must handle both: this cheap 409 guard, or a run that is
    /// accepted here and then ends <c>Failed</c> carrying <c>marketplaceSyncAlreadyRunning</c> because the
    /// worker's Postgres advisory lock was already taken. Codes a failed run can carry in
    /// <c>MarketplaceSyncRun.Error</c> (and in <c>MarketplaceAccount.LastSyncError</c>):
    /// <list type="bullet">
    ///   <item><c>marketplaceAccountNotFound</c> — the account was deleted between enqueue and run; <c>args</c>: <c>accountId</c></item>
    ///   <item><c>marketplaceSyncAlreadyRunning</c> — the advisory lock is held by another run</item>
    ///   <item><c>marketplaceCredentialsUnreadable</c> — the stored key cannot be decrypted</item>
    ///   <item><c>marketplaceCredentialsInvalid</c> — the marketplace rejected the credentials; <c>args</c>: <c>marketplaceStatus</c>, optional <c>marketplaceResponse</c></item>
    ///   <item><c>marketplaceOrdersNotSupported</c> — <c>Orders</c> scope on a provider without the <c>Orders</c> capability</item>
    ///   <item><c>marketplaceApiError</c> — any other marketplace or unexpected failure; <c>args</c>: <c>marketplaceStatus</c>, optional <c>marketplaceResponse</c> when it came from the API</item>
    ///   <item><c>marketplaceSyncInterrupted</c> — the run was left <c>Running</c> by an application shutdown and reconciled on the next start</item>
    /// </list>
    /// Requires <c>integrations.sync</c>.
    /// </remarks>
    [HttpPost("accounts/{id:guid}/sync")]
    [Authorize(Policy = Permissions.Integrations.Sync)]
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
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200). An unknown account id
    /// yields an empty page rather than a 404. A failed run carries its machine-readable <c>error</c>
    /// (code + args) — see <c>POST accounts/{id}/sync</c> for the codes. An <c>Orders</c>-scope run also
    /// reports per-posting skips in <c>skippedOrders</c> (first 100, counted in full by <c>ordersSkipped</c>):
    /// <list type="bullet">
    ///   <item><c>marketplaceOrderWarehouseNotMapped</c> — the posting's marketplace warehouse has no WMS mapping</item>
    ///   <item><c>marketplaceOrderCardNotMapped</c> — an item has no card, or its card is unmapped; the offending <c>offerIds</c> travel with the entry</item>
    /// </list>
    /// Requires <c>integrations.view</c>.
    /// </remarks>
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

    /// <summary>Runs by id, for polling several accounts from one dialog.</summary>
    /// <remarks>
    /// Query param: <c>ids</c> (repeatable, at most 50; an empty list answers 200 with an empty array).
    /// Unknown ids are simply absent from the response — the caller knows what it asked for.
    /// Returns 422 <c>outOfRange</c> on <c>ids</c> above the limit, <c>args</c>: <c>max</c>.
    /// Run payloads carry the same <c>error</c> and <c>skippedOrders</c> codes as <c>GET accounts/{id}/sync-runs</c>.
    /// Requires <c>integrations.view</c>.
    /// </remarks>
    [HttpGet("sync-runs")]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<List<MarketplaceSyncRunDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncRunsByIds([FromQuery] Guid[] ids, CancellationToken ct)
    {
        if (ids.Length > MaxBatchAccounts)
            return UnprocessableEntity(nameof(ids), ErrorCode.OutOfRange,
                $"At most {MaxBatchAccounts} runs can be requested at once.",
                new Dictionary<string, object> { ["max"] = MaxBatchAccounts });

        if (ids.Length == 0)
            return Ok(new List<MarketplaceSyncRunDto>());

        var runs = await db.MarketplaceSyncRuns
            .Where(r => ids.Contains(r.Id))
            .ProjectTo<MarketplaceSyncRunDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(runs);
    }

    // ---------- order sync ----------

    /// <summary>Accounts that can import orders — the source list for the sync dialog.</summary>
    /// <remarks>
    /// Only active accounts whose provider declares the <c>Orders</c> capability are listed; an unreadable
    /// key is surfaced as <c>credentialsUnreadable</c> on the row instead of dropping it, so the dialog can
    /// explain why the account cannot be ticked. Produces no error of its own beyond
    /// 403 <c>permissionDenied</c>. Requires <c>integrations.sync</c>.
    /// </remarks>
    [HttpGet("accounts/order-sync-targets")]
    [Authorize(Policy = Permissions.Integrations.Sync)]
    [ProducesResponseType<List<MarketplaceOrderSyncTargetDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderSyncTargets(CancellationToken ct)
    {
        var accounts = await db.MarketplaceAccounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                Dto = new MarketplaceOrderSyncTargetDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Type = a.Type,
                    IsSyncRunning = a.SyncRuns.Any(r => r.Status == MarketplaceSyncStatus.Running),
                    MappedWarehouseCount = a.Warehouses.Count(w => !w.IsArchived && w.WarehouseId != null),
                    UnmappedWarehouseCount = a.Warehouses.Count(w => !w.IsArchived && w.WarehouseId == null),
                    UnmappedCardCount = a.Cards.Count(c => !c.IsArchived && c.CatalogItemId == null),
                },
                a.Type,
                a.ApiKeyProtected,
            })
            .ToListAsync(ct);

        // capabilities and key readability are provider/runtime facts, so they resolve after the query
        var targets = new List<MarketplaceOrderSyncTargetDto>();
        foreach (var row in accounts)
        {
            if (!providers.TryGet(row.Type, out var provider)
                || !provider.Capabilities.HasFlag(MarketplaceCapabilities.Orders))
                continue;

            row.Dto.Capabilities = provider.Capabilities;
            row.Dto.CredentialsUnreadable = !protector.TryUnprotect(row.ApiKeyProtected, out _);
            targets.Add(row.Dto);
        }

        return Ok(targets);
    }

    /// <summary>Queues order sync for several accounts at once; each account succeeds or fails on its own.</summary>
    /// <remarks>
    /// Body: <c>SyncOrdersRequest</c> — <c>accountIds</c> (duplicates are collapsed, at most 50).
    /// Each account is checked independently and the call answers 202 with <c>SyncOrdersResponse</c>:
    /// queued accounts in <c>items</c> as <c>{ accountId, syncRunId }</c>, the rest in <c>failedItems</c> as
    /// <c>{ accountId, accountName, error }</c> carrying the real code:
    /// <list type="bullet">
    ///   <item><c>marketplaceAccountNotFound</c> — the id matched no account (<c>accountName</c> is null)</item>
    ///   <item><c>marketplaceAccountInactive</c> — the account is disabled</item>
    ///   <item><c>marketplaceOrdersNotSupported</c> — no provider, or the provider lacks the <c>Orders</c> capability</item>
    ///   <item><c>marketplaceCredentialsUnreadable</c> — the stored key cannot be decrypted</item>
    ///   <item><c>marketplaceSyncAlreadyRunning</c> — a run for this account is still <c>Running</c></item>
    /// </list>
    /// There is no transaction: accounts queued before a later one fails stay queued.
    /// The only whole-request errors are 422 <c>outOfRange</c> on <c>accountIds</c> above the limit
    /// (<c>args</c>: <c>max</c>) and 403 <c>permissionDenied</c> when <c>integrations.sync</c> is missing —
    /// 403 is never per item.
    /// A queued run can still end <c>Failed</c> with <c>marketplaceSyncAlreadyRunning</c> when the worker
    /// loses the advisory lock, so a client that only handles the <c>failedItems</c> form misses half the cases;
    /// the full list of run-level codes is on <c>POST accounts/{id}/sync</c>.
    /// </remarks>
    [HttpPost("accounts/sync-orders")]
    [Authorize(Policy = Permissions.Integrations.Sync)]
    [ProducesResponseType<SyncOrdersResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SyncOrders([FromBody] SyncOrdersRequest request, CancellationToken ct)
    {
        var accountIds = request.AccountIds.Distinct().ToList();
        if (accountIds.Count > MaxBatchAccounts)
            return UnprocessableEntity(nameof(request.AccountIds), ErrorCode.OutOfRange,
                $"At most {MaxBatchAccounts} accounts can be synced at once.",
                new Dictionary<string, object> { ["max"] = MaxBatchAccounts });

        var accounts = await db.MarketplaceAccounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var running = await db.MarketplaceSyncRuns
            .Where(r => accountIds.Contains(r.MarketplaceAccountId) && r.Status == MarketplaceSyncStatus.Running)
            .Select(r => r.MarketplaceAccountId)
            .ToListAsync(ct);

        var started = new List<SyncOrdersStartedItem>();
        var failed = new List<SyncOrdersFailedItem>();

        void Fail(Guid accountId, string? name, ErrorCode code, string message) =>
            failed.Add(new SyncOrdersFailedItem
            {
                AccountId = accountId,
                AccountName = name,
                Error = AppProblems.MakeError(code, message),
            });

        foreach (var accountId in accountIds)
        {
            if (!accounts.TryGetValue(accountId, out var account))
            {
                Fail(accountId, null, ErrorCode.MarketplaceAccountNotFound, "Marketplace account not found.");
                continue;
            }

            if (!account.IsActive)
            {
                Fail(accountId, account.Name, ErrorCode.MarketplaceAccountInactive, "The account is disabled.");
                continue;
            }

            if (!providers.TryGet(account.Type, out var provider)
                || !provider.Capabilities.HasFlag(MarketplaceCapabilities.Orders))
            {
                Fail(accountId, account.Name, ErrorCode.MarketplaceOrdersNotSupported,
                    "This marketplace provider does not support order sync.");
                continue;
            }

            if (!protector.TryUnprotect(account.ApiKeyProtected, out _))
            {
                Fail(accountId, account.Name, ErrorCode.MarketplaceCredentialsUnreadable,
                    "The stored API key can no longer be decrypted.");
                continue;
            }

            if (running.Contains(accountId))
            {
                Fail(accountId, account.Name, ErrorCode.MarketplaceSyncAlreadyRunning,
                    "A sync is already running for this account.");
                continue;
            }

            var runId = await EnqueueSyncAsync(account, MarketplaceSyncScope.Orders, ct);
            started.Add(new SyncOrdersStartedItem { AccountId = accountId, SyncRunId = runId });
        }

        return StatusCode(StatusCodes.Status202Accepted,
            new SyncOrdersResponse { Items = started, FailedItems = failed });
    }

    // ---------- warehouses ----------

    /// <summary>Marketplace warehouses of an account with their WMS mapping (paginated, searchable).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 50, max 200),
    /// <c>includeArchived</c> (default false), <c>sortBy</c> (default <c>Name</c>), <c>sortOrder</c> (default <c>Asc</c>).
    /// An unknown account id yields an empty page rather than a 404.
    /// Requires <c>integrations.view</c>; 403 <c>permissionDenied</c> otherwise.
    /// </remarks>
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
    /// <remarks>
    /// Body: <c>SetWarehouseMappingRequest</c> — <c>warehouseId</c> (null clears). Errors:
    /// <list type="bullet">
    ///   <item>404 <c>marketplaceWarehouseNotFound</c></item>
    ///   <item>422 <c>warehouseNotFound</c> on <c>warehouseId</c> — no WMS warehouse with that id</item>
    /// </list>
    /// Requires <c>integrations.map</c>.
    /// </remarks>
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
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 50, max 200), <c>searchString</c>,
    /// <c>mappingState</c> (default <c>All</c>; <c>Unmapped</c>, <c>Mapped</c>, <c>ArchivedItem</c>),
    /// <c>includeArchived</c> (default false), <c>catalogItemId</c>, <c>sortBy</c> (default <c>Name</c>),
    /// <c>sortOrder</c> (default <c>Asc</c>). An unknown account id yields an empty page rather than a 404.
    /// Requires <c>integrations.view</c>; 403 <c>permissionDenied</c> otherwise.
    /// </remarks>
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
        [FromQuery] Guid? catalogItemId = null,
        CancellationToken ct = default)
    {
        var query = db.MarketplaceCards
            .Where(c => c.MarketplaceAccountId == id)
            .WhereMatchesSearch(c => c.SearchString, searchString);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        if (catalogItemId is not null)
        {
            query = query.Where(c => c.CatalogItemId == catalogItemId);
        }

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
    /// <remarks>
    /// Body: <c>SetCardMappingRequest</c> — <c>catalogItemId</c> (null clears). Errors:
    /// <list type="bullet">
    ///   <item>404 <c>marketplaceCardNotFound</c></item>
    ///   <item>422 <c>catalogItemNotFound</c> on <c>catalogItemId</c></item>
    ///   <item>422 <c>marketplaceCardMappingTypeNotAllowed</c> on <c>catalogItemId</c> — the target is a <c>ProductGroup</c></item>
    ///   <item>422 <c>marketplaceCardMappingArchivedItem</c> on <c>catalogItemId</c> — the target is archived</item>
    /// </list>
    /// The archive check only applies when setting a mapping: an item archived afterwards keeps it.
    /// Clearing (<c>catalogItemId: null</c>) skips all three target checks. Requires <c>integrations.map</c>.
    /// </remarks>
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
    /// <remarks>
    /// Anything ambiguous (no candidate or more than one) is left for a human, so the operation never fails
    /// on a card: 404 <c>marketplaceAccountNotFound</c> is the only error besides 403 <c>permissionDenied</c>.
    /// Requires <c>integrations.map</c>.
    /// </remarks>
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
    /// <remarks>
    /// Archived cards and cards of inactive accounts are excluded. Takes no parameters and produces no error
    /// beyond 403 <c>permissionDenied</c>. Requires <c>integrations.view</c>.
    /// </remarks>
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

    /// <summary>A dialog with more accounts than this is a mistake, not a use case.</summary>
    private const int MaxBatchAccounts = 50;

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
