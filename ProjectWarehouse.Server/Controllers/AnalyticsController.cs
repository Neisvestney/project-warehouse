using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Analytics;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/analytics")]
public class AnalyticsController(
    IAnalyticsChannelsService channels,
    IAnalyticsAbcService abc,
    IAnalyticsPayoutsService payouts,
    IAnalyticsSettingsService settings,
    IChangeLogService<AnalyticsSettingsDto> changeLog) : AppControllerBase
{
    /// <summary>Per-channel summary: orders, units, cancellations, returns, revenue, discount and shares.</summary>
    /// <remarks>
    /// Query params come from <c>ChannelsSummaryRequest</c>: <c>from</c> and <c>to</c> (required, inclusive
    /// days), <c>includeMarketplaces</c> (default true; false leaves every shop out),
    /// <c>marketplaceAccountIds</c> (empty — every shop), <c>includeDirect</c> (default true),
    /// <c>directTagIds</c> (Direct orders carrying any of them), <c>moneyMode</c> (<c>price</c> or
    /// <c>payout</c>, default <c>price</c>).
    /// An order belongs to the day of its <c>EffectiveDate</c>, cut in the zone sent as <c>X-Time-Zone</c>
    /// (otherwise the server's); the applied zone comes back as <c>timeZoneId</c>. A marketplace order is
    /// judged by the marketplace status only: <c>delivering</c> / <c>delivered</c> is a sale,
    /// <c>cancelled</c> a cancellation. A Direct order is a sale when <c>assembled</c> / <c>shipped</c>.
    /// Rows: one per shop, then — with <c>includeDirect</c> — the whole Direct channel, one row per tag met in
    /// the period and the untagged row; tag rows overlap, the Direct row counts every order once.
    /// Money is listed per currency and never summed across them; <c>payoutCoverage</c> is filled only in
    /// the <c>payout</c> mode. <c>returnsMaturityDays</c> echoes the applied setting.
    /// Requires <c>analytics.view</c>.
    /// Returns 422 <c>required</c> on a missing <c>from</c> / <c>to</c>, 422 <c>invalidValue</c> on
    /// <c>to</c> when it is earlier than <c>from</c>, and 422 <c>outOfRange</c> on <c>to</c> when the
    /// period exceeds 366 days (no <c>args</c> on any of them).
    /// </remarks>
    [HttpGet("channels/summary")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsSummary(
        [FromQuery] ChannelsSummaryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetSummaryAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Orders or units per interval, one series per shop plus the whole Direct channel.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c>, plus <c>step</c> (<c>day</c> / <c>week</c> /
    /// <c>month</c>; omitted — day up to 31 days, week up to six months, month beyond) and <c>measure</c>
    /// (<c>orders</c> / <c>units</c>, default <c>orders</c>). Only sales count: a marketplace order in
    /// <c>delivering</c> / <c>delivered</c>, a Direct one <c>assembled</c> / <c>shipped</c>, each on the day of its
    /// <c>EffectiveDate</c>. Weeks start on Monday. Intervals are clamped to the period and flagged:
    /// <c>isPartial</c> when the step sticks out of it, <c>isCurrent</c> when it holds today, <c>isFuture</c> when
    /// it starts after today — values of a future interval are null. The applied step comes back as <c>step</c>.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("channels/timeseries")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsTimeseriesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsTimeseries(
        [FromQuery] ChannelsTimeseriesRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetTimeseriesAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>The returns card: shares by sale cohort, volume by return date, reasons and compensations.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c> (<c>includeDirect</c> and <c>directTagIds</c>
    /// are ignored — Direct has no returns), <c>moneyMode</c> and <c>step</c> as in <c>channels/timeseries</c>.
    /// Only returns counted as such (<c>customerReturn</c> / <c>partialRefusal</c>, not cancelled) of a sale
    /// order take part. <c>accounts</c>, <c>topReasons</c> and <c>compensations</c> are tied to the sales dated in
    /// the period, whenever the item came back; <c>series</c> is tied to <c>ReturnedAt</c> — a return with no
    /// such date is absent from it. Returned money is listed in the <c>price</c> mode only.
    /// <c>returnsImmature</c> marks a period ending closer to today than <c>returnsMaturityDays</c>.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("channels/returns")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsReturnsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsReturns(
        [FromQuery] ChannelsReturnsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetReturnsAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Cancelled and all counted orders of each shop per interval, for the cancellation chart.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c> (<c>includeDirect</c> and <c>directTagIds</c>
    /// are ignored — the cancellation card covers shops only) and <c>step</c> as in <c>channels/timeseries</c>.
    /// An order falls into the interval of its <c>EffectiveDate</c>, as in the summary, so the series add up to its
    /// cancellation counts. <c>series</c> holds the cancelled orders, <c>orders</c> the sales plus cancellations of
    /// the same shop in the same order — the share is <c>series / orders</c>. Values of a future interval are null.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("channels/cancellations")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsCancellationsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsCancellations(
        [FromQuery] ChannelsCancellationsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetCancellationsAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Sales per interval next to their cancellations and returns, for the combined chart.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c>, <c>step</c> and <c>measure</c> as in
    /// <c>channels/timeseries</c>; the measure changes <c>sales</c> only. Every count of a point is tied to the order
    /// date: cancellations are the cancelled orders of the interval, <c>returnedUnits</c> the returns of the
    /// interval's sales whenever the item came back. The cancellation share is
    /// <c>cancellations / (saleOrders + cancellations)</c> over every selected channel, the return share
    /// <c>returnedUnits / shopUnits</c> — Direct has no returns, so its units stay out of the base; both shop
    /// counts are null when no shop is selected. <c>returnsImmature</c> marks an interval ending closer to today
    /// than <c>returnsMaturityDays</c>. Values of a future interval are null.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("channels/losses")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsLossesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsLosses(
        [FromQuery] ChannelsLossesRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetLossesAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Catalog items ranked by sales of the selected channels.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c> — one shop, Direct alone or several channels
    /// are asked for through it; <c>by</c> (<c>units</c> / <c>money</c>, default <c>units</c>),
    /// <c>moneyMode</c> as in <c>channels/summary</c>, <c>currencyCode</c> (omitted or unknown — the currency
    /// with the most sale lines; the ones met come back as <c>currencies</c>) and <c>take</c> (1..1000,
    /// omitted — every ranked item). A bundle ranks as itself. Units add up every selected channel; money exists
    /// on shop lines only, in the applied currency. Items whose ranking value is zero are left out, and
    /// <c>totalItems</c> counts the ranked ones before <c>take</c>. Shop lines with no catalog item are not
    /// ranked; their count is <c>unlinkedLines</c>.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>, plus 422
    /// <c>validationError</c> on <c>take</c> out of range.
    /// </remarks>
    [HttpGet("channels/top-items")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsTopItemsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsTopItems(
        [FromQuery] ChannelsTopItemsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetTopItemsAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Average orders or units per weekday, by channel and by Direct tag.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c> and <c>measure</c> as in
    /// <c>channels/timeseries</c>. A value is the sum over every such weekday of the period divided by how many
    /// of them the period holds, Monday first. Only finished days count, today excluded: <c>countedTo</c> is the
    /// last day averaged, null when the period has no finished day yet (every value is then null).
    /// Rows follow <c>channels/summary</c>: one per shop, then — with <c>includeDirect</c> — the whole Direct
    /// channel, one per tag met in the period and the untagged row; tag rows overlap. <c>total</c> is every
    /// selected channel together, each Direct order counted once.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("channels/weekdays")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<ChannelsWeekdaysDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelsWeekdays(
        [FromQuery] ChannelsWeekdaysRequest request,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await channels.GetWeekdaysAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>ABC and XYZ classes of the catalog items, cards or articles sold in the period: tiles, matrix, Pareto and a table page.</summary>
    /// <remarks>
    /// Query params: the shared filter of <c>channels/summary</c>; <c>basis</c> (<c>units</c> / <c>price</c> /
    /// <c>payout</c>, default <c>units</c>) — a money basis analyses shops only, in one currency:
    /// <c>currencyCode</c>, omitted or unknown — the one with the most sale lines, the ones met come back as
    /// <c>currencies</c>. <c>subject</c> (<c>catalogItem</c> / <c>card</c> / <c>article</c>, default
    /// <c>catalogItem</c>) picks what a row is: the catalog item a line was imported with, the card of one account,
    /// or every selected account's cards sharing an offer id compared trimmed and case-blind; a card or an article
    /// analyses shops only, and each row's <c>subject</c> carries its name, offer id, image and accounts.
    /// <c>unlinkedLines</c> counts the lines with no catalog item, or with no card. A bundle is an item of its
    /// own; items with no sales are not analysed. Items are ranked
    /// by value descending, ties by id or article, and classed by the cumulative share of the items before them against
    /// <c>abcBoundaryA</c> / <c>abcBoundaryB</c>. XYZ always counts units over the same channels and items, by the
    /// full <c>xyzStep</c> intervals of the period that are already over; with fewer than
    /// <c>xyzMinIntervals</c> of them (<c>xyzIntervals</c>) no item has an XYZ class. <c>xyzFromFirstSale</c>
    /// (default true) starts the series of an item that never sold before the period at the interval of its first
    /// sale, and an item left with fewer than <c>xyzMinIntervals</c> gets no class. <c>classes</c>,
    /// <c>matrix</c> and <c>pareto</c> cover the whole analysis; <c>abcClass</c>, <c>xyzClass</c>,
    /// <c>searchString</c>, <c>page</c> (default 1) and <c>pageSize</c> (default 25, max 200) narrow
    /// <c>items</c> only, whose <c>rank</c> stays the place in the whole analysis. <c>payoutCoverage</c> is filled
    /// for the <c>payout</c> basis only. <c>settings</c> echoes the applied parameters.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>, plus 422
    /// <c>validationError</c> on <c>page</c> / <c>pageSize</c> out of range.
    /// </remarks>
    [HttpGet("abc")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<AbcDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbc([FromQuery] AbcRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await abc.GetAbcAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>ABC and XYZ class of every item of the <c>abc</c> table month by month, for the year up to the period's end.</summary>
    /// <remarks>
    /// Query params: those of <c>abc</c> without <c>page</c> / <c>pageSize</c>. The rows are the items the period's
    /// analysis leaves after <c>abcClass</c>, <c>xyzClass</c> and <c>searchString</c>, in its rank order, with no
    /// paging. <c>months</c> are the 12 months up to the one holding <c>to</c>, or yesterday when <c>to</c> is not
    /// over; each is a window of <c>windowDays</c> (91) ending on the month's last day, cut at <c>to</c> and before
    /// today (<c>isPartial</c>). A window is analysed on its own the way <c>abc</c> analyses a period, over the same
    /// channels, basis and the period's currency; its XYZ counts the window's full <c>xyzStep</c> intervals
    /// (<c>xyzIntervals</c>) and stays empty below <c>xyzMinIntervals</c>. A cell is empty when the item sold
    /// nothing in the window. Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("abc/timeline")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<AbcTimelineDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbcTimeline([FromQuery] AbcFilterRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await abc.GetTimelineAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>What the marketplaces owe for postings in transit and delivered, and what they accrued in the period.</summary>
    /// <remarks>
    /// Query params: <c>from</c>/<c>to</c> — both or neither; neither means all time, and the response then carries the
    /// earliest journal day of the selected shops as <c>from</c> and today as <c>to</c>; <c>includeMarketplaces</c> and
    /// <c>marketplaceAccountIds</c> as in <c>channels/summary</c>. A posting counts as accrued once the journal holds a <c>sale</c> accrual for it. Debt buckets
    /// ignore the period: <c>inTransit</c> — <c>delivering</c> postings with no sale; <c>deliveredNotAccrued</c> —
    /// <c>delivered</c> ones with no sale younger than <c>payoutNotAccruedDays</c>; <c>notAccruedByMarketplace</c> —
    /// the older ones, out of the debt. Age counts from the posting's <c>EffectiveDate</c>. A delivered posting dated
    /// before the shop's <c>coveredFrom</c> (first day of its journal, a Moscow day) with no sale is in no bucket and is
    /// counted in <c>uncoveredPostings</c>. Debt amounts are estimates: Σ price × quantity × the shop's
    /// <c>payoutRatio</c> — journal net without return logistics over sale price of the postings whose sale was accrued
    /// in the last <c>payoutRatioWindowDays</c> days, reversed ones excluded — and are null when there is no such
    /// posting or the net is not positive. <c>inTransit</c> is the sum of <c>inTransitByAge</c>. <c>accrued</c> is
    /// the sum of every journal line dated in the period, shop-wide ones included, and <c>categories</c> splits it by
    /// category, <c>byPosting</c> false for shop-wide lines. Money is per currency; <c>totals</c> sum each shop's own
    /// estimate and leave out shops with no ratio.
    /// <c>settings</c> echoes the applied parameters.
    /// Requires <c>analytics.view</c>. Same 422 codes as <c>channels/summary</c>.
    /// </remarks>
    [HttpGet("payouts")]
    [TimeZoneAware]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<PayoutsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayouts([FromQuery] PayoutsRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await payouts.GetPayoutsAsync(User, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Analytics calculation parameters: stored values, system defaults and what applies.</summary>
    /// <remarks>
    /// A null in <c>saved</c> means the field follows the system default. <c>version</c> is to be sent back
    /// with the update. Requires <c>analytics.view</c>.
    /// </remarks>
    [HttpGet("settings")]
    [Authorize(Policy = Permissions.Analytics.View)]
    [ProducesResponseType<AnalyticsSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct = default) =>
        Ok(await settings.GetSettingsAsync(ct));

    /// <summary>Writes every analytics parameter at once; null restores the default.</summary>
    /// <remarks>
    /// The settings are shared by the whole system and apply at once to any period, past ones included.
    /// Requires <c>analytics.settings</c>.
    /// Returns 422 <c>required</c> on a missing <c>version</c>; 422 <c>validationError</c> on a value outside
    /// its range, on <c>payoutAgeBoundaries</c> holding no or more than 5 values and on
    /// <c>payoutAgeBoundaries[i]</c> outside 1..365; 422 <c>invalidValue</c> on a boundary with more than one
    /// decimal place, on <c>abcBoundaryB</c> not above <c>abcBoundaryA</c>, on <c>xyzBoundaryY</c> not above
    /// <c>xyzBoundaryX</c> (both compared as they will apply, defaults included) and on
    /// <c>payoutAgeBoundaries</c> not strictly ascending; 409 <c>analyticsSettingsModified</c> when someone
    /// else saved since <c>version</c> (no <c>args</c> on any of them).
    /// </remarks>
    [HttpPut("settings")]
    [Authorize(Policy = Permissions.Analytics.Settings)]
    [ProducesResponseType<AnalyticsSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateAnalyticsSettingsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var (before, after) = await settings.UpdateSettingsAsync(request, GetCurrentUserId(), ct);
            await changeLog.CompareAndSaveToChangelog(before, after);
            return Ok(after);
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (AnalyticsSettingsConflictException)
        {
            return Conflict(ErrorCode.AnalyticsSettingsModified,
                "The analytics settings were changed by someone else. Reload them and apply the change again.");
        }
    }
}
