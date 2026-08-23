using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// Auto-mapping rules. They are global — one set shared by every marketplace account — so they live
/// outside the per-account routes of <see cref="MarketplacesController"/>.
/// </summary>
[Route("api/integrations/marketplaces/auto-map-rules")]
public class MarketplaceAutoMapRulesController(
    ApplicationDbContext db,
    IMapper mapper,
    IRealtimeNotifier realtime,
    IChangeLogService<MarketplaceAutoMapRuleDto> changeLog) : AppControllerBase
{
    /// <summary>The rules are versioned as one set — the lock and the event use an empty id.</summary>
    private static readonly Guid RulesEntityId = Guid.Empty;

    /// <summary>All auto-mapping rules, in the order they are applied.</summary>
    /// <remarks>
    /// Takes no parameters and is not paginated — the rule set is small by design. Ordered by
    /// <c>priority</c> descending, then by <c>id</c>. Requires <c>integrations.view</c>;
    /// 403 <c>permissionDenied</c> otherwise.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Integrations.View)]
    [ProducesResponseType<List<MarketplaceAutoMapRuleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var rules = await db.MarketplaceAutoMapRules
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id)
            .ProjectTo<MarketplaceAutoMapRuleDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(rules);
    }

    /// <summary>Create an auto-mapping rule.</summary>
    /// <remarks>
    /// Errors:
    /// <list type="bullet">
    ///   <item>422 <c>required</c> on <c>value</c> — the value is blank</item>
    ///   <item>422 <c>marketplaceAutoMapRuleInvalidRegex</c> on <c>value</c> — the pattern does not compile</item>
    ///   <item>422 <c>catalogItemNotFound</c> on <c>catalogItemId</c></item>
    ///   <item>422 <c>marketplaceCardMappingTypeNotAllowed</c> on <c>catalogItemId</c> — a product group cannot back a card</item>
    ///   <item>422 <c>marketplaceCardMappingArchivedItem</c> on <c>catalogItemId</c> — the target is archived</item>
    /// </list>
    /// Requires <c>integrations.map</c>.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<MarketplaceAutoMapRuleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRule([FromBody] SaveAutoMapRuleRequest request, CancellationToken ct)
    {
        if (await ValidateAsync(request, ct) is { } error)
            return error;

        var now = DateTime.UtcNow;
        var rule = new MarketplaceAutoMapRule
        {
            Field = request.Field,
            Operator = request.Operator,
            Value = request.Value.Trim(),
            CatalogItemId = request.CatalogItemId,
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.MarketplaceAutoMapRules.Add(rule);
        await db.SaveChangesAsync(ct);

        var dto = await ProjectAsync(rule.Id, ct);
        await changeLog.CompareAndSaveToChangelog(null, dto, MarketplaceActions.RuleCreated);
        await PublishRulesChangedAsync(ct);

        return Ok(dto);
    }

    /// <summary>Update an auto-mapping rule.</summary>
    /// <remarks>
    /// 404 <c>marketplaceAutoMapRuleNotFound</c> when the rule is gone, plus the same 422 codes as creation.
    /// Requires <c>integrations.map</c>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType<MarketplaceAutoMapRuleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] SaveAutoMapRuleRequest request, CancellationToken ct)
    {
        var rule = await db.MarketplaceAutoMapRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return NotFound(ErrorCode.MarketplaceAutoMapRuleNotFound, "Auto-mapping rule not found.");

        if (await ValidateAsync(request, ct) is { } error)
            return error;

        var before = await ProjectAsync(id, ct);

        rule.Field = request.Field;
        rule.Operator = request.Operator;
        rule.Value = request.Value.Trim();
        rule.CatalogItemId = request.CatalogItemId;
        rule.IsEnabled = request.IsEnabled;
        rule.Priority = request.Priority;
        rule.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var after = await ProjectAsync(id, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, MarketplaceActions.RuleUpdated);
        await PublishRulesChangedAsync(ct);

        return Ok(after);
    }

    /// <summary>Delete an auto-mapping rule. Cards it already mapped keep their mapping.</summary>
    /// <remarks>
    /// 404 <c>marketplaceAutoMapRuleNotFound</c> when the rule is gone. Requires <c>integrations.map</c>.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Integrations.Map)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var rule = await db.MarketplaceAutoMapRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return NotFound(ErrorCode.MarketplaceAutoMapRuleNotFound, "Auto-mapping rule not found.");

        var before = await ProjectAsync(id, ct);

        db.MarketplaceAutoMapRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(before, null, MarketplaceActions.RuleDeleted);
        await PublishRulesChangedAsync(ct);

        return NoContent();
    }

    // ---------- helpers ----------

    // The changelog fires on a single rule; the page watches the set, so it needs waking explicitly.
    private ValueTask PublishRulesChangedAsync(CancellationToken ct) =>
        realtime.PublishEntityChangedAsync(AppEntityType.MarketplaceAutoMapRules, RulesEntityId, User, ct);

    private async Task<MarketplaceAutoMapRuleDto> ProjectAsync(Guid id, CancellationToken ct) =>
        await db.MarketplaceAutoMapRules
            .Where(r => r.Id == id)
            .ProjectTo<MarketplaceAutoMapRuleDto>(mapper.ConfigurationProvider)
            .FirstAsync(ct);

    private async Task<IActionResult?> ValidateAsync(SaveAutoMapRuleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
            return UnprocessableEntity(nameof(request.Value), ErrorCode.Required, "The value is required.");

        if (request.Operator == MarketplaceRuleOperator.Regex)
        {
            try
            {
                MarketplaceRuleMatcher.BuildRegex(request.Value.Trim());
            }
            catch (ArgumentException)
            {
                return UnprocessableEntity(nameof(request.Value), ErrorCode.MarketplaceAutoMapRuleInvalidRegex,
                    "The regular expression is invalid.");
            }
        }

        var target = await db.CatalogItems
            .Where(i => i.Id == request.CatalogItemId)
            .Select(i => new { i.Type, i.IsArchived })
            .FirstOrDefaultAsync(ct);

        if (target is null)
            return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.CatalogItemNotFound,
                "Catalog item not found.");

        if (!MarketplaceMapping.MappableTypes.Contains(target.Type))
            return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.MarketplaceCardMappingTypeNotAllowed,
                "A product group cannot be an order component and cannot back a marketplace card.");

        if (target.IsArchived)
            return UnprocessableEntity(nameof(request.CatalogItemId), ErrorCode.MarketplaceCardMappingArchivedItem,
                "The catalog item is archived.");

        return null;
    }
}
