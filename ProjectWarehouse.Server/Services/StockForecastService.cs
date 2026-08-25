using System.Security.Claims;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Forecast;

namespace ProjectWarehouse.Server.Services;

public class StockForecastService(
    ApplicationDbContext db,
    IMapper mapper,
    IUserQueryFilterService userFilter,
    IInventoryService inventoryService,
    IWarehouseTimeZoneResolver timeZones) : IStockForecastService
{
    /// <summary>Virtual types hold no stock, so there is nothing to forecast for them.</summary>
    private static readonly CatalogItemType[] PhysicalTypes = [CatalogItemType.Standard, CatalogItemType.Unit];

    /// <summary>Where the rows come from — the only thing the two entry points disagree about.</summary>
    private sealed record ForecastSource(
        Guid WarehouseId,
        IQueryable<StockMovement> Movements,
        IReadOnlyCollection<Guid> StockWarehouseIds);

    private sealed record CatalogFilter(
        string? SearchString = null,
        IReadOnlyList<CatalogItemType>? Types = null,
        IReadOnlyList<Guid>? TagIds = null,
        bool? IsArchived = null,
        bool OnlyWarnings = false);

    /// <summary>A computed row before it is sorted; the catalog fields are the sort keys.</summary>
    private sealed record ForecastEntry(
        CatalogItemType Type,
        string Name,
        string FullName,
        string Article,
        StockForecastDto Forecast);

    private sealed record ForecastComputation(
        IReadOnlyList<ForecastEntry> Entries,
        StockForecastOptions Options,
        int WarehouseWarningDays);

    public async Task<StockForecastListDto> GetListAsync(
        ClaimsPrincipal user,
        StockForecastListRequest request,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Model validation rejects a missing warehouse before the action runs.
        var warehouseId = request.WarehouseId!.Value;

        var filter = new CatalogFilter(
            request.SearchString, request.CatalogItemTypes, request.TagIds, request.IsArchived, request.OnlyWarnings);

        var computed = await ComputeAsync(
            await SourceForAsync(user, warehouseId, ct), filter, restrictToIds: null, options: null, ct);

        var sorted = Sort(computed.Entries, request.SortBy, request.SortOrder).ToList();
        var pageEntries = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var rows = await ToRowsAsync(pageEntries, ct);

        return new StockForecastListDto
        {
            Items = new Paginated<StockForecastRowDto>
            {
                Items = rows,
                Total = sorted.Count,
                Page = page,
                PageSize = pageSize,
            },
            WindowDays = computed.Options.WindowDays,
            UseWeightedConsumption = computed.Options.UseWeightedConsumption,
            TimeZoneId = computed.Options.TimeZoneId,
            WarehouseWarningDays = computed.WarehouseWarningDays,
        };
    }

    public async Task<IReadOnlyDictionary<Guid, StockForecastDto>> GetForItemsAsync(
        ClaimsPrincipal user,
        Guid warehouseId,
        IReadOnlyCollection<Guid> catalogItemIds,
        StockForecastOptions? options = null,
        CancellationToken ct = default)
    {
        if (catalogItemIds.Count == 0)
            return new Dictionary<Guid, StockForecastDto>();

        var computed = await ComputeAsync(
            await SourceForAsync(user, warehouseId, ct), new CatalogFilter(), catalogItemIds, options, ct);

        return computed.Entries.ToDictionary(e => e.Forecast.CatalogItemId, e => e.Forecast);
    }

    public async Task<IReadOnlyList<StockForecastRowDto>> ComputeForWarehouseAsync(
        Guid warehouseId,
        StockForecastScope scope,
        StockForecastOptions? options = null,
        CancellationToken ct = default)
    {
        var filter = new CatalogFilter(
            Types: scope.CatalogItemTypes,
            IsArchived: scope.ExcludeArchived ? false : null,
            OnlyWarnings: scope.OnlyWarnings);

        // No query filter and no warehouse narrowing: this entry checks no permissions by contract.
        var source = new ForecastSource(warehouseId, db.StockMovements, [warehouseId]);
        var computed = await ComputeAsync(source, filter, restrictToIds: null, options, ct);

        return await ToRowsAsync(Sort(computed.Entries, StockForecastSortBy.Default, SortOrder.Asc).ToList(), ct);
    }

    public async Task<StockForecastSettingsDto> GetSettingsAsync(
        ClaimsPrincipal user, Guid warehouseId, CancellationToken ct = default) =>
        await BuildSettingsAsync(await LoadWarehouseAsync(warehouseId, ct), ct);

    public async Task<StockForecastSettingsDto> UpdateSettingsAsync(
        ClaimsPrincipal user,
        Guid warehouseId,
        UpdateStockForecastSettingsRequest request,
        CancellationToken ct = default)
    {
        var warehouse = await LoadWarehouseAsync(warehouseId, ct);

        // Unlike the header, a stored identifier is a setting somebody typed: accepting a broken one
        // would leave the warehouse silently cutting its days by the server zone forever.
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId)
            && !TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
            throw new ValidationException("timeZoneId", ErrorCode.InvalidValue,
                "Unknown IANA time zone identifier.");

        warehouse.StockWarningDays = request.StockWarningDays;
        warehouse.ConsumptionWindowDays = request.ConsumptionWindowDays;
        warehouse.UseWeightedConsumption = request.UseWeightedConsumption;
        warehouse.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId;

        await db.SaveChangesAsync(ct);

        return await BuildSettingsAsync(warehouse, ct);
    }

    public async Task SetOverrideAsync(
        ClaimsPrincipal user, SetStockWarningOverrideRequest request, CancellationToken ct = default)
    {
        // The access rule answers "may you touch this warehouse", not "does it exist": an unscoped
        // warehouses.edit is allowed straight off the claim, and an unknown id would reach the insert
        // and surface as a foreign key violation instead of a field error.
        await EnsureWarehouseExistsAsync(request.WarehouseId, ct);

        var type = await db.CatalogItems
            .Where(c => c.Id == request.CatalogItemId)
            .Select(c => (CatalogItemType?)c.Type)
            .FirstOrDefaultAsync(ct);

        if (type is null)
            throw new ValidationException("catalogItemId", ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        if (!PhysicalTypes.Contains(type.Value))
            throw new ValidationException("catalogItemId", ErrorCode.InvalidValue,
                "Only Standard and Unit items hold stock and can carry a threshold.");

        var existing = await db.CatalogItemStockWarnings
            .FirstOrDefaultAsync(
                o => o.CatalogItemId == request.CatalogItemId && o.WarehouseId == request.WarehouseId, ct);

        if (request.WarningDays is not { } warningDays)
        {
            // Reset deletes the row rather than writing the warehouse's value, so a later change to the
            // warehouse setting still reaches the item.
            if (existing is not null) db.CatalogItemStockWarnings.Remove(existing);
        }
        else if (existing is not null)
        {
            existing.WarningDays = warningDays;
        }
        else
        {
            db.CatalogItemStockWarnings.Add(new CatalogItemStockWarning
            {
                CatalogItemId = request.CatalogItemId,
                WarehouseId = request.WarehouseId,
                WarningDays = warningDays,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The single implementation both entry points reach the moment their permissions have parted:
    /// one number, whether it is shown on the page or sent in an alert.
    /// </summary>
    private async Task<ForecastComputation> ComputeAsync(
        ForecastSource source,
        CatalogFilter filter,
        IReadOnlyCollection<Guid>? restrictToIds,
        StockForecastOptions? options,
        CancellationToken ct)
    {
        var warehouse = await db.Warehouses
            .Where(w => w.Id == source.WarehouseId)
            .Select(w => new
            {
                w.StockWarningDays,
                w.ConsumptionWindowDays,
                w.UseWeightedConsumption,
            })
            .FirstOrDefaultAsync(ct);

        if (warehouse is null)
            throw WarehouseNotFound();

        options ??= await ResolveOptionsAsync(
            source.WarehouseId, warehouse.ConsumptionWindowDays, warehouse.UseWeightedConsumption, ct);

        var warehouseWarningDays = StockForecastCalculator.ResolveWarningDays(null, warehouse.StockWarningDays);

        var offset = TimeSpan.FromMinutes(options.OffsetMinutes);
        var today = DateOnly.FromDateTime(DateTime.UtcNow + offset);
        var from = today.AddDays(-(options.WindowDays - 1));
        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);

        var stock = await inventoryService.GetCurrentStockAsync(
            source.StockWarehouseIds, source.WarehouseId, null, null, restrictToIds, ct);

        var consumption = await LoadConsumptionAsync(
            source, restrictToIds, fromUtc, toUtc, options, today, ct);

        // A row exists when the item has stock or consumption; an empty catalog is never unfolded into
        // the forecast. Zero stock with consumption is exactly the row a buyer needs to see.
        var candidateIds = stock.Keys.Concat(consumption.Keys).Distinct().ToList();
        if (candidateIds.Count == 0)
            return new ForecastComputation([], options, warehouseWarningDays);

        var items = await LoadCatalogItemsAsync(candidateIds, filter, ct);
        var overrides = await db.CatalogItemStockWarnings
            .Where(o => o.WarehouseId == source.WarehouseId && candidateIds.Contains(o.CatalogItemId))
            .ToDictionaryAsync(o => o.CatalogItemId, o => o.WarningDays, ct);

        var empty = new int[options.WindowDays];
        var entries = new List<ForecastEntry>(items.Count);

        foreach (var item in items)
        {
            var itemOverride = overrides.TryGetValue(item.Id, out var days) ? days : (int?)null;
            var warningDays = StockForecastCalculator.ResolveWarningDays(itemOverride, warehouse.StockWarningDays);
            var result = StockForecastCalculator.Calculate(
                stock.GetValueOrDefault(item.Id),
                consumption.GetValueOrDefault(item.Id) ?? empty,
                options,
                warningDays);

            if (filter.OnlyWarnings && !StockForecastCalculator.IsWarning(result.Status))
                continue;

            entries.Add(new ForecastEntry(item.Type, item.Name, item.FullName, item.Article,
                new StockForecastDto
                {
                    CatalogItemId = item.Id,
                    Stock = stock.GetValueOrDefault(item.Id),
                    DailyConsumption = result.DailyConsumption,
                    ConsumedInWindow = result.ConsumedInWindow,
                    DaysLeft = result.DaysLeft,
                    WarningDays = warningDays,
                    IsWarningOverridden = itemOverride is not null,
                    Status = result.Status,
                }));
        }

        return new ForecastComputation(entries, options, warehouseWarningDays);
    }

    private async Task<StockForecastOptions> ResolveOptionsAsync(
        Guid warehouseId, int? windowSetting, bool useWeighted, CancellationToken ct)
    {
        var zone = await timeZones.ResolveAsync(warehouseId, ct);

        return new StockForecastOptions
        {
            WindowDays = StockForecastCalculator.ResolveWindowDays(windowSetting),
            UseWeightedConsumption = useWeighted,
            TimeZoneId = zone.IanaId(),
            OffsetMinutes = zone.CurrentOffsetMinutes(),
        };
    }

    /// <summary>
    /// Out quantities per item per day of the window. <c>TransferOut</c> is left out on purpose: the
    /// goods did not leave the company, and the matching <c>TransferIn</c> on the receiving warehouse
    /// would burn the same item a second time when it actually ships.
    /// </summary>
    private static async Task<Dictionary<Guid, int[]>> LoadConsumptionAsync(
        ForecastSource source,
        IReadOnlyCollection<Guid>? restrictToIds,
        DateTime fromUtc,
        DateTime toUtc,
        StockForecastOptions options,
        DateOnly today,
        CancellationToken ct)
    {
        var query = source.Movements
            .Where(m => m.WarehouseId == source.WarehouseId)
            .Where(m => m.Direction == StockMovementDirection.Out)
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtc);

        if (restrictToIds is not null)
            query = query.Where(m => restrictToIds.Contains(m.CatalogItemId));

        var rows = await query
            .GroupBy(m => new { m.CatalogItemId, Date = m.CreatedAt.AddMinutes(options.OffsetMinutes).Date })
            .Select(g => new { g.Key.CatalogItemId, g.Key.Date, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        var byItem = new Dictionary<Guid, int[]>();
        foreach (var row in rows)
        {
            var age = today.DayNumber - DateOnly.FromDateTime(row.Date).DayNumber;
            if (age < 0 || age >= options.WindowDays) continue;

            if (!byItem.TryGetValue(row.CatalogItemId, out var days))
                byItem[row.CatalogItemId] = days = new int[options.WindowDays];

            days[age] += row.Quantity;
        }

        return byItem;
    }

    private sealed record CatalogRow(Guid Id, CatalogItemType Type, string Name, string FullName, string Article);

    private async Task<List<CatalogRow>> LoadCatalogItemsAsync(
        IReadOnlyList<Guid> candidateIds, CatalogFilter filter, CancellationToken ct)
    {
        var query = db.CatalogItems
            .Where(c => candidateIds.Contains(c.Id))
            .Where(c => PhysicalTypes.Contains(c.Type))
            .WhereMatchesSearch(c => c.SearchString, filter.SearchString);

        if (filter.Types is { Count: > 0 } types)
            query = query.Where(c => types.Contains(c.Type));

        if (filter.TagIds is { Count: > 0 } tagIds)
            query = query.Where(c => c.Tags.Any(t => tagIds.Contains(t.Id)));

        if (filter.IsArchived is { } isArchived)
            query = query.Where(c => c.IsArchived == isArchived);

        return await query
            .Select(c => new CatalogRow(c.Id, c.Type, c.Name, c.FullName, c.Article))
            .ToListAsync(ct);
    }

    /// <summary>Catalog summaries are loaded for the rows that survived paging, not for the whole set.</summary>
    private async Task<List<StockForecastRowDto>> ToRowsAsync(
        IReadOnlyList<ForecastEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return [];

        var ids = entries.Select(e => e.Forecast.CatalogItemId).ToList();
        var items = await db.CatalogItems
            .Where(c => ids.Contains(c.Id))
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(c => c.Id, ct);

        return entries
            .Where(e => items.ContainsKey(e.Forecast.CatalogItemId))
            .Select(e => new StockForecastRowDto
            {
                CatalogItemId = e.Forecast.CatalogItemId,
                Stock = e.Forecast.Stock,
                DailyConsumption = e.Forecast.DailyConsumption,
                ConsumedInWindow = e.Forecast.ConsumedInWindow,
                DaysLeft = e.Forecast.DaysLeft,
                WarningDays = e.Forecast.WarningDays,
                IsWarningOverridden = e.Forecast.IsWarningOverridden,
                Status = e.Forecast.Status,
                CatalogItem = items[e.Forecast.CatalogItemId],
            })
            .ToList();
    }

    /// <summary>
    /// Default order: everything on fire first, ascending by days left inside each group, so zero stock
    /// naturally floats to the top. An explicit column replaces the whole rule — except that "never runs
    /// out" stays at the bottom either way.
    /// </summary>
    private static IEnumerable<ForecastEntry> Sort(
        IReadOnlyList<ForecastEntry> entries, StockForecastSortBy sortBy, SortOrder sortOrder)
    {
        if (sortBy == StockForecastSortBy.Default)
            return entries
                .OrderByDescending(e => StockForecastCalculator.IsWarning(e.Forecast.Status))
                .ThenBy(e => e.Forecast.DaysLeft is null)
                .ThenBy(e => e.Forecast.DaysLeft ?? 0)
                .ThenBy(e => e.FullName)
                .ThenBy(e => e.Forecast.CatalogItemId);

        var desc = sortOrder == SortOrder.Desc;

        IOrderedEnumerable<ForecastEntry> ordered = sortBy switch
        {
            StockForecastSortBy.Type => By(entries, e => e.Type, desc),
            StockForecastSortBy.Article => By(entries, e => e.Article, desc),
            StockForecastSortBy.Stock => By(entries, e => e.Forecast.Stock, desc),
            StockForecastSortBy.DailyConsumption => By(entries, e => e.Forecast.DailyConsumption, desc),
            StockForecastSortBy.DaysLeft => ThenBy(
                entries.OrderBy(e => e.Forecast.DaysLeft is null), e => e.Forecast.DaysLeft ?? 0, desc),
            _ => By(entries, e => e.FullName, desc),
        };

        return ordered.ThenBy(e => e.Forecast.CatalogItemId);
    }

    private static IOrderedEnumerable<T> By<T, TKey>(IEnumerable<T> source, Func<T, TKey> key, bool desc) =>
        desc ? source.OrderByDescending(key) : source.OrderBy(key);

    private static IOrderedEnumerable<T> ThenBy<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> key, bool desc) =>
        desc ? source.ThenByDescending(key) : source.ThenBy(key);

    private async Task<ForecastSource> SourceForAsync(
        ClaimsPrincipal user, Guid warehouseId, CancellationToken ct)
    {
        var movements = await userFilter.GetStockMovementsAsync(user, ct);
        var warehouseIds = await (await userFilter.GetWarehousesAsync(user, ct))
            .Select(w => w.Id)
            .ToListAsync(ct);

        return new ForecastSource(warehouseId, movements, warehouseIds);
    }

    private async Task<Warehouse> LoadWarehouseAsync(Guid warehouseId, CancellationToken ct) =>
        await db.Warehouses.FirstOrDefaultAsync(w => w.Id == warehouseId, ct) ?? throw WarehouseNotFound();

    private async Task EnsureWarehouseExistsAsync(Guid warehouseId, CancellationToken ct)
    {
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct))
            throw WarehouseNotFound();
    }

    private static ValidationException WarehouseNotFound() =>
        new("warehouseId", ErrorCode.WarehouseNotFound, "Warehouse not found.");

    private async Task<StockForecastSettingsDto> BuildSettingsAsync(Warehouse warehouse, CancellationToken ct)
    {
        var zone = await timeZones.ResolveAsync(warehouse.Id, ct);

        return new StockForecastSettingsDto
        {
            WarehouseId = warehouse.Id,
            StockWarningDays = warehouse.StockWarningDays,
            ConsumptionWindowDays = warehouse.ConsumptionWindowDays,
            UseWeightedConsumption = warehouse.UseWeightedConsumption,
            TimeZoneId = warehouse.TimeZoneId,
            DefaultWarningDays = StockForecastCalculator.DefaultWarningDays,
            DefaultWindowDays = StockForecastCalculator.DefaultWindowDays,
            EffectiveWarningDays = StockForecastCalculator.ResolveWarningDays(null, warehouse.StockWarningDays),
            EffectiveWindowDays = StockForecastCalculator.ResolveWindowDays(warehouse.ConsumptionWindowDays),
            EffectiveTimeZoneId = zone.IanaId(),
        };
    }
}
