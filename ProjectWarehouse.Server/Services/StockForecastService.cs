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
using ProjectWarehouse.Server.Models.Warehouses;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;

namespace ProjectWarehouse.Server.Services;

public class StockForecastService(
    ApplicationDbContext db,
    IMapper mapper,
    IUserQueryFilterService userFilter,
    IInventoryService inventoryService,
    IWarehouseTimeZoneResolver timeZones,
    IChangeLogService<WarehouseDto> warehouseChangeLog,
    IChangeLogService changeLog) : IStockForecastService
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
        bool OnlyWarnings = false,
        bool? IsVariation = null,
        bool HideVariationMembers = false);

    /// <summary>A computed row before it is sorted; the catalog fields are the sort keys.</summary>
    private sealed record ForecastEntry(
        CatalogItemType Type,
        string Name,
        string FullName,
        string Article,
        StockForecastDto Forecast,
        IReadOnlyList<StockForecastDto>? Members = null);

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
            request.SearchString, request.CatalogItemTypes, request.TagIds, request.IsArchived, request.OnlyWarnings,
            request.IsVariation, HideVariationMembers: request.IsVariation is null);

        var computed = await ComputeAsync(
            await SourceForAsync(user, warehouseId, ct), filter, restrictToIds: null, options: null,
            request.AccountForAssembly, ct);

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
            await SourceForAsync(user, warehouseId, ct), new CatalogFilter(), catalogItemIds, options,
            accountForAssembly: false, ct);

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
            OnlyWarnings: scope.OnlyWarnings,
            IsVariation: false);

        // No query filter and no warehouse narrowing: this entry checks no permissions by contract.
        var source = new ForecastSource(warehouseId, db.StockMovements, [warehouseId]);
        var computed = await ComputeAsync(source, filter, restrictToIds: null, options, accountForAssembly: false, ct);

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
        var beforeDto = await WarehouseDtoAsync(warehouseId, ct);

        var timeZoneId = TimeZoneIds.Normalize(request.TimeZoneId);

        // Unlike the header, a stored identifier is a setting somebody typed: accepting a broken one
        // would leave the warehouse silently cutting its days by the server zone forever.
        if (timeZoneId is not null && !TimeZoneIds.IsKnown(timeZoneId))
            throw new ValidationException("timeZoneId", ErrorCode.InvalidValue,
                "Unknown IANA time zone identifier.");

        warehouse.StockWarningDays = request.StockWarningDays;
        warehouse.ConsumptionWindowDays = request.ConsumptionWindowDays;
        warehouse.UseWeightedConsumption = request.UseWeightedConsumption;
        warehouse.TimeZoneId = timeZoneId;

        await db.SaveChangesAsync(ct);

        await warehouseChangeLog.CompareAndSaveToChangelog(beforeDto, await WarehouseDtoAsync(warehouseId, ct));

        return await BuildSettingsAsync(warehouse, ct);
    }

    public async Task SetOverrideAsync(
        ClaimsPrincipal user, SetStockWarningOverrideRequest request, CancellationToken ct = default)
    {
        // The access rule answers "may you touch this warehouse", not "does it exist": an unscoped
        // warehouses.edit is allowed straight off the claim, and an unknown id would reach the insert
        // and surface as a foreign key violation instead of a field error.
        var warehouseName = await db.Warehouses
            .Where(w => w.Id == request.WarehouseId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(ct) ?? throw WarehouseNotFound();

        var type = await db.CatalogItems
            .Where(c => c.Id == request.CatalogItemId)
            .Select(c => (CatalogItemType?)c.Type)
            .FirstOrDefaultAsync(ct);

        if (type is null)
            throw new ValidationException("catalogItemId", ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        if (!PhysicalTypes.Contains(type.Value) && type != CatalogItemType.Variation)
            throw new ValidationException("catalogItemId", ErrorCode.InvalidValue,
                "Only Standard, Unit and Variation items are forecast and can carry a threshold.");

        var existing = await db.CatalogItemStockWarnings
            .FirstOrDefaultAsync(
                o => o.CatalogItemId == request.CatalogItemId && o.WarehouseId == request.WarehouseId, ct);

        var beforeOverride = OverrideSnapshot(request, warehouseName, existing?.WarningDays);

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

        // Both sides are non-null so clearing reads as "threshold went back to inherited" rather than as
        // a deleted catalog item — the entry hangs on the item, whose own DTO has no per-warehouse field.
        await changeLog.CompareAndSaveToChangelog(
            AppEntityType.CatalogItem, request.CatalogItemId,
            beforeOverride, OverrideSnapshot(request, warehouseName, request.WarningDays),
            action: request.WarningDays is null ? ForecastActions.OverrideCleared : ForecastActions.OverrideSet,
            actionData: new { warehouseId = request.WarehouseId, warehouseName });
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
        bool accountForAssembly,
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

        var variations = filter.IsVariation == false
            ? []
            : await LoadVariationsAsync(filter, restrictToIds, ct);

        // A requested variation needs its members' numbers even when the members themselves were not asked for.
        var physicalIds = restrictToIds?
            .Concat(variations.SelectMany(v => v.MemberIds))
            .Distinct()
            .ToList();

        var stock = await inventoryService.GetCurrentStockAsync(
            source.StockWarehouseIds, source.WarehouseId, null, null, physicalIds, ct);

        // Snapshot before the assembly reservation is folded in below: the zero-stock lookback
        // reconstructs the physical past, and assembly demand is a present-day reservation, not
        // something that existed on those earlier days.
        var physicalStock = new Dictionary<Guid, int>(stock);

        var movement = await LoadMovementAsync(
            source, physicalIds, fromUtc, toUtc, options, today, ct);

        var assembly = accountForAssembly
            ? await LoadAssemblyDemandAsync(source.WarehouseId, ct)
            : new AssemblyDemand([], []);
        var assemblyDemand = assembly.Items;

        // Left negative on purpose: more is reserved for assembly than physically sits on the shelf,
        // and that shortfall is exactly what the flag exists to surface.
        foreach (var (catalogItemId, quantity) in assemblyDemand)
            stock[catalogItemId] = stock.GetValueOrDefault(catalogItemId) - quantity;

        // A row exists when the item has stock or consumption; an empty catalog is never unfolded into
        // the forecast. Zero stock with consumption is exactly the row a buyer needs to see.
        var candidateIds = stock.Keys.Concat(movement.DailyOut.Keys).Concat(assemblyDemand.Keys).ToHashSet();

        var liveVariations = variations
            .Where(v => v.MemberIds.Any(candidateIds.Contains) || assembly.Variations.ContainsKey(v.Row.Id))
            .ToList();

        if (candidateIds.Count == 0 && liveVariations.Count == 0)
            return new ForecastComputation([], options, warehouseWarningDays);

        var physicalCandidates = filter.IsVariation == true
            ? []
            : candidateIds.Where(id => restrictToIds is null || restrictToIds.Contains(id)).ToList();
        var items = physicalCandidates.Count == 0
            ? []
            : await LoadCatalogItemsAsync(physicalCandidates, filter, ct);

        var overrideIds = items.Select(i => i.Id)
            .Concat(liveVariations.SelectMany(v => v.MemberIds.Append(v.Row.Id)))
            .Distinct()
            .ToList();
        var overrides = await db.CatalogItemStockWarnings
            .Where(o => o.WarehouseId == source.WarehouseId && overrideIds.Contains(o.CatalogItemId))
            .ToDictionaryAsync(o => o.CatalogItemId, o => o.WarningDays, ct);

        var empty = new int[options.WindowDays];
        var entries = new List<ForecastEntry>(items.Count + liveVariations.Count);

        StockForecastDto Forecast(Guid id, int itemStock, int itemPhysicalStock, int[] dailyOut, int[] dailyNet)
        {
            var itemOverride = overrides.TryGetValue(id, out var days) ? days : (int?)null;
            var warningDays = StockForecastCalculator.ResolveWarningDays(itemOverride, warehouse.StockWarningDays);

            var zeroStockAge = StockForecastCalculator.FindLastZeroStockAge(itemPhysicalStock, dailyNet);
            var stockedDays = StockForecastCalculator.FindStockedDays(itemPhysicalStock, dailyNet, dailyOut);

            var result = StockForecastCalculator.Calculate(itemStock, dailyOut, options, warningDays, stockedDays);

            return new StockForecastDto
            {
                CatalogItemId = id,
                Stock = itemStock,
                DailyConsumption = result.DailyConsumption,
                ConsumedInWindow = result.ConsumedInWindow,
                DaysLeft = result.DaysLeft,
                WarningDays = warningDays,
                IsWarningOverridden = itemOverride is not null,
                Status = result.Status,
                DaysSinceLastZeroStock = zeroStockAge,
                OutOfStockDays = stockedDays.Count(d => !d),
            };
        }

        StockForecastDto ItemForecast(Guid id) =>
            Forecast(id,
                stock.GetValueOrDefault(id),
                physicalStock.GetValueOrDefault(id),
                movement.DailyOut.GetValueOrDefault(id) ?? empty,
                movement.DailyNet.GetValueOrDefault(id) ?? empty);

        void AddEntry(CatalogRow item, StockForecastDto forecast, IReadOnlyList<StockForecastDto>? members = null)
        {
            if (filter.OnlyWarnings && !StockForecastCalculator.IsWarning(forecast.Status))
                return;

            entries.Add(new ForecastEntry(item.Type, item.Name, item.FullName, item.Article, forecast, members));
        }

        foreach (var item in items)
            AddEntry(item, ItemForecast(item.Id));

        // A variation component on an assembly order has no member picked yet, so it reserves against the
        // variation's total only; each member row keeps just the demand that names it directly.
        foreach (var variation in liveVariations)
        {
            var memberIds = variation.MemberIds;
            var variationStock = memberIds.Sum(id => stock.GetValueOrDefault(id))
                                 - assembly.Variations.GetValueOrDefault(variation.Row.Id);
            var variationPhysicalStock = memberIds.Sum(id => physicalStock.GetValueOrDefault(id));
            var variationDailyOut = SumDays(memberIds, movement.DailyOut, options.WindowDays);
            var variationDailyNet = SumDays(memberIds, movement.DailyNet, options.WindowDays);

            AddEntry(variation.Row,
                Forecast(variation.Row.Id, variationStock, variationPhysicalStock, variationDailyOut, variationDailyNet),
                memberIds.Select(ItemForecast).ToList());
        }

        // Only members of a variation row that made it into the result: when the variation was filtered out,
        // the member stays on its own, so a warning on it is never lost.
        if (filter.HideVariationMembers)
        {
            var nestedIds = entries
                .SelectMany(e => e.Members ?? [])
                .Select(m => m.CatalogItemId)
                .ToHashSet();
            entries.RemoveAll(e => e.Members is null && nestedIds.Contains(e.Forecast.CatalogItemId));
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

    private sealed record MovementData(
        Dictionary<Guid, int[]> DailyOut, Dictionary<Guid, int[]> DailyNet);

    /// <summary>
    /// Two views of the same window's movements, per item per day (index 0 = today):
    /// <c>DailyOut</c> — Out quantities only, the consumption feeding the average. <c>TransferOut</c> is
    /// left out of it on purpose, same reasoning as everywhere else in the forecast: the goods did not
    /// leave the company, and the matching <c>TransferIn</c> on the receiving warehouse would burn the
    /// same item a second time when it actually ships.
    /// <c>DailyNet</c> — every direction, signed (in - out), the raw balance change used to walk the
    /// stock backward and find the last day it sat at zero. Transfers count here: they moved physical
    /// units off or onto this warehouse's shelf regardless of what the consumption definition ignores.
    /// </summary>
    private static async Task<MovementData> LoadMovementAsync(
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
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtc);

        if (restrictToIds is not null)
            query = query.Where(m => restrictToIds.Contains(m.CatalogItemId));

        var rows = await query
            .GroupBy(m => new
            {
                m.CatalogItemId,
                m.Direction,
                Date = m.CreatedAt.AddMinutes(options.OffsetMinutes).Date,
            })
            .Select(g => new { g.Key.CatalogItemId, g.Key.Direction, g.Key.Date, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        var dailyOut = new Dictionary<Guid, int[]>();
        var dailyNet = new Dictionary<Guid, int[]>();

        foreach (var row in rows)
        {
            var age = today.DayNumber - DateOnly.FromDateTime(row.Date).DayNumber;
            if (age < 0 || age >= options.WindowDays) continue;

            var sign = row.Direction is StockMovementDirection.In or StockMovementDirection.TransferIn ? 1 : -1;
            AddToDay(dailyNet, row.CatalogItemId, age, sign * row.Quantity, options.WindowDays);

            if (row.Direction == StockMovementDirection.Out)
                AddToDay(dailyOut, row.CatalogItemId, age, row.Quantity, options.WindowDays);
        }

        return new MovementData(dailyOut, dailyNet);
    }

    private static void AddToDay(
        Dictionary<Guid, int[]> byItem, Guid catalogItemId, int age, int quantity, int windowDays)
    {
        if (!byItem.TryGetValue(catalogItemId, out var days))
            byItem[catalogItemId] = days = new int[windowDays];

        days[age] += quantity;
    }

    /// <summary>
    /// The not-yet-picked quantity of every box component on orders currently in <c>Assembly</c> on this
    /// warehouse, exploded down to physical items: a Bundle component recurses into its own components,
    /// a Variation component is kept apart under the variation's own id — it has no single member to credit,
    /// and the picker hasn't chosen one yet.
    /// </summary>
    private sealed record AssemblyDemand(Dictionary<Guid, int> Items, Dictionary<Guid, int> Variations);

    private async Task<AssemblyDemand> LoadAssemblyDemandAsync(Guid warehouseId, CancellationToken ct)
    {
        var components = await db.AssemblyTaskBoxComponents
            .Where(c => c.AssemblyTaskBox.AssemblyTask.Order.WarehouseId == warehouseId
                        && c.AssemblyTaskBox.AssemblyTask.Order.Status == OrderStatus.Assembly)
            .Select(c => new
            {
                c.CatalogItemId,
                c.CatalogItem.Type,
                c.Quantity,
                Fulfilled = c.Fulfillments
                    .Sum(f => f.UnitInventoryItemId != null || f.BundleComponents.Count > 0 ? 1 : f.Quantity),
            })
            .ToListAsync(ct);

        // Loaded once and reused for every component: bundle nesting is shallow in practice, and one
        // query beats a network round trip per node visited while exploding it.
        var bundleChildren = await db.BundleComponents
            .Select(bc => new { bc.BundleId, bc.ComponentId, bc.Quantity, bc.Component.Type })
            .ToListAsync(ct);
        var childrenByBundle = bundleChildren
            .GroupBy(bc => bc.BundleId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(bc => (bc.ComponentId, bc.Quantity, bc.Type)).ToList());

        var demand = new AssemblyDemand([], []);
        foreach (var component in components)
        {
            var remaining = component.Quantity - component.Fulfilled;
            if (remaining <= 0) continue;

            AddExplodedDemand(component.CatalogItemId, component.Type, remaining, childrenByBundle, demand, []);
        }

        return demand;
    }

    private static void AddExplodedDemand(
        Guid catalogItemId,
        CatalogItemType type,
        int quantity,
        IReadOnlyDictionary<Guid, List<(Guid ComponentId, int Quantity, CatalogItemType Type)>> childrenByBundle,
        AssemblyDemand demand,
        HashSet<Guid> visiting)
    {
        if (type == CatalogItemType.Variation)
        {
            demand.Variations[catalogItemId] = demand.Variations.GetValueOrDefault(catalogItemId) + quantity;
            return;
        }

        if (type != CatalogItemType.Bundle)
        {
            demand.Items[catalogItemId] = demand.Items.GetValueOrDefault(catalogItemId) + quantity;
            return;
        }

        if (!visiting.Add(catalogItemId)) return; // bundles cannot legally cycle, but stay defensive

        if (childrenByBundle.TryGetValue(catalogItemId, out var children))
            foreach (var child in children)
                AddExplodedDemand(child.ComponentId, child.Type, quantity * child.Quantity, childrenByBundle, demand, visiting);

        visiting.Remove(catalogItemId);
    }

    private sealed record CatalogRow(Guid Id, CatalogItemType Type, string Name, string FullName, string Article);

    private sealed record VariationRow(CatalogRow Row, List<Guid> MemberIds);

    private static int[] SumDays(IEnumerable<Guid> ids, Dictionary<Guid, int[]> byItem, int windowDays)
    {
        var sum = new int[windowDays];
        foreach (var id in ids)
            if (byItem.TryGetValue(id, out var days))
                for (var i = 0; i < windowDays; i++)
                    sum[i] += days[i];

        return sum;
    }

    /// <summary>
    /// Variations forecast as the sum of their members. Only those made of physical members qualify: a Bundle
    /// member holds no stock of its own, and exploding it would count shared components more than once.
    /// </summary>
    private async Task<List<VariationRow>> LoadVariationsAsync(
        CatalogFilter filter, IReadOnlyCollection<Guid>? restrictToIds, CancellationToken ct)
    {
        var query = ApplyCommonFilter(
            db.CatalogItems
                .Where(c => c.Type == CatalogItemType.Variation)
                .Where(c => c.VariationMembers.Any()
                            && c.VariationMembers.All(m => PhysicalTypes.Contains(m.Item.Type))),
            filter);

        if (restrictToIds is not null)
            query = query.Where(c => restrictToIds.Contains(c.Id));

        var rows = await query
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Name,
                c.FullName,
                c.Article,
                MemberIds = c.VariationMembers.Select(m => m.ItemId).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new VariationRow(new CatalogRow(r.Id, r.Type, r.Name, r.FullName, r.Article), r.MemberIds))
            .ToList();
    }

    private static IQueryable<CatalogItem> ApplyCommonFilter(IQueryable<CatalogItem> query, CatalogFilter filter)
    {
        query = query.WhereMatchesSearch(c => c.SearchString, filter.SearchString);

        if (filter.TagIds is { Count: > 0 } tagIds)
            query = query.Where(c => c.Tags.Any(t => tagIds.Contains(t.Id)));

        if (filter.IsArchived is { } isArchived)
            query = query.Where(c => c.IsArchived == isArchived);

        return query;
    }

    private async Task<List<CatalogRow>> LoadCatalogItemsAsync(
        IReadOnlyList<Guid> candidateIds, CatalogFilter filter, CancellationToken ct)
    {
        var query = ApplyCommonFilter(
            db.CatalogItems
                .Where(c => candidateIds.Contains(c.Id))
                .Where(c => PhysicalTypes.Contains(c.Type)),
            filter);

        if (filter.Types is { Count: > 0 } types)
            query = query.Where(c => types.Contains(c.Type));

        return await query
            .Select(c => new CatalogRow(c.Id, c.Type, c.Name, c.FullName, c.Article))
            .ToListAsync(ct);
    }

    /// <summary>Catalog summaries are loaded for the rows that survived paging, not for the whole set.</summary>
    private async Task<List<StockForecastRowDto>> ToRowsAsync(
        IReadOnlyList<ForecastEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return [];

        var ids = entries
            .SelectMany(e => (e.Members ?? []).Append(e.Forecast))
            .Select(f => f.CatalogItemId)
            .Distinct()
            .ToList();
        var items = await db.CatalogItems
            .Where(c => ids.Contains(c.Id))
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(c => c.Id, ct);

        StockForecastRowDto ToRow(StockForecastDto f, List<StockForecastRowDto>? members) => new()
        {
            CatalogItemId = f.CatalogItemId,
            Stock = f.Stock,
            DailyConsumption = f.DailyConsumption,
            ConsumedInWindow = f.ConsumedInWindow,
            DaysLeft = f.DaysLeft,
            WarningDays = f.WarningDays,
            IsWarningOverridden = f.IsWarningOverridden,
            Status = f.Status,
            DaysSinceLastZeroStock = f.DaysSinceLastZeroStock,
            OutOfStockDays = f.OutOfStockDays,
            CatalogItem = items[f.CatalogItemId],
            Members = members,
        };

        return entries
            .Where(e => items.ContainsKey(e.Forecast.CatalogItemId))
            .Select(e => ToRow(e.Forecast, e.Members?
                .Where(m => items.ContainsKey(m.CatalogItemId))
                // Catalog order, the same every variation-member list keeps.
                .OrderBy(m => items[m.CatalogItemId].IsArchived)
                .ThenBy(m => items[m.CatalogItemId].FullName)
                .ThenBy(m => m.CatalogItemId)
                .Select(m => ToRow(m, null))
                .ToList()))
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

    /// <summary>The changelog snapshot of a warehouse, the same shape the warehouse endpoints journal.</summary>
    private async Task<WarehouseDto> WarehouseDtoAsync(Guid warehouseId, CancellationToken ct) =>
        await db.Warehouses
            .ProjectTo<WarehouseDto>(mapper.ConfigurationProvider)
            .AsSplitQuery()
            .FirstAsync(w => w.Id == warehouseId, ct);

    private static StockWarningOverrideDto OverrideSnapshot(
        SetStockWarningOverrideRequest request, string warehouseName, int? warningDays) =>
        new()
        {
            CatalogItemId = request.CatalogItemId,
            WarehouseId = request.WarehouseId,
            WarehouseName = warehouseName,
            WarningDays = warningDays,
        };

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
