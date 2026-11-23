using System.Runtime.CompilerServices;
using Neisvestney.Marketplaces.YandexMarket;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public sealed partial class YandexMarketClient
{
    public async IAsyncEnumerable<IReadOnlyList<ExternalReturn>> GetReturnsAsync(ExternalReturnQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (query.CompensationStatus is not null) yield break;
        var history = query.DateKind == ExternalReturnDateKind.Returned;
        var to = history ? DateTime.UtcNow : query.To ?? DateTime.UtcNow;
        string? token = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var response = (await api.GetReturnsAsync(CampaignId, token, 100, null, null, null, null,
                query.Since is { } since ? Utc(since.Date.AddDays(-1)) : null, Utc(to.Date), ct)).Result
                ?? throw new InvalidOperationException("Yandex Market returned no returns result.");
            yield return response.Returns
                .Where(r => !history || r.CreationDate is { } created
                    && created.UtcDateTime >= query.Since && created.UtcDateTime <= query.To)
                .SelectMany(MapReturn).ToArray();
            token = Next(response.Paging?.NextPageToken, seen);
            if (token is not null) await PauseAsync(ct);
        } while (token is not null);
    }

    public static IEnumerable<ExternalReturn> MapReturn(ReturnDTO value)
    {
        var shipment = value.ShipmentStatus?.ToString();
        var refund = value.RefundStatus?.ToString();
        foreach (var item in value.Items)
        {
            var decision = item.Decisions?.FirstOrDefault();
            var reason = decision?.ReasonType?.ToString();
            var subreason = decision?.SubreasonType?.ToString();
            var reasonCode = reason is null ? null : $"{reason}/{subreason}";
            yield return new($"{Id(value.Id)}:{item.ShopSku}", Id(value.OrderId), item.MarketSku?.ToString(), item.ShopSku,
                MarketplaceReturnScheme.Fbs, "FBS",
                value.ReturnType == ReturnType.UNREDEEMED ? MarketplaceReturnKind.FullRefusal : MarketplaceReturnKind.CustomerReturn,
                value.ReturnType.ToString(), checked((int)item.Count), null, null,
                string.Join("; ", item.Decisions?.Select(d => d.Comment).Where(c => !string.IsNullOrWhiteSpace(c)) ?? []),
                reason is null ? null : LossReason(reason, subreason), reasonCode, shipment, ShipmentStatusName(shipment),
                refund is "CANCELLED" or "REJECTED" || shipment is "CANCELLED" or "EXPIRED",
                value.UpdateDate?.UtcDateTime, value.CreationDate?.UtcDateTime, null, null, null, null, null,
                RawRefundStatus: refund);
        }
    }

    private static string? ShipmentStatusName(string? value) => value switch
    {
        "CREATED" => "Оформлен",
        "IN_TRANSIT" => "В пути",
        "READY_FOR_PICKUP" => "Готов к выдаче",
        "PICKED" => "Получен продавцом",
        "UTILIZED" => "Утилизирован",
        "CANCELLED" => "Отменён",
        "EXPIRED" => "Срок возврата истёк",
        _ => value,
    };
}
