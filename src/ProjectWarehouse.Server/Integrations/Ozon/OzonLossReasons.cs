using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Ozon cancellation and return reasons collapsed into <see cref="MarketplaceLossReason"/>. Matched by wording:
/// returns carry no reason id at all, and cancellations share most of their wording with refusal-type returns.
/// A wording Ozon adds later lands as <see cref="MarketplaceLossReason.Unknown"/> and is logged by the client.
/// </summary>
public static class OzonLossReasons
{
    private static readonly Dictionary<string, MarketplaceLossReason> Reasons =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Покупатель отменил заказ"] = MarketplaceLossReason.BuyerChangedMind,
            ["Покупатель передумал"] = MarketplaceLossReason.BuyerChangedMind,
            ["Покупатель отменил заказ: нашел дешевле"] = MarketplaceLossReason.BuyerFoundCheaper,
            ["Покупатель отменил заказ: не устроил срок доставки"] = MarketplaceLossReason.BuyerDeliveryTerms,
            ["Покупатель отказался при вручении: товар не подошел"] = MarketplaceLossReason.BuyerDidNotFit,
            ["Покупатель не забрал заказ"] = MarketplaceLossReason.BuyerNotPickedUp,
            ["Покупатель не предоставил паспортные данные"] = MarketplaceLossReason.BuyerOther,

            ["Товар закончился на складе"] = MarketplaceLossReason.OutOfStock,
            ["Вы не отгрузили заказ вовремя"] = MarketplaceLossReason.NotShippedInTime,
            ["Вы отменили заказ"] = MarketplaceLossReason.SellerCancelled,
            ["Товар не работает / брак"] = MarketplaceLossReason.Defect,
            ["Товар сломался при эксплуатации"] = MarketplaceLossReason.Defect,
            ["Товар поврежден, но упаковка цела"] = MarketplaceLossReason.Defect,
            ["Покупатель отказался при вручении: недоволен качеством товара"] = MarketplaceLossReason.PoorQuality,
            ["Покупатель отказался при вручении: в заказе не тот товар"] = MarketplaceLossReason.WrongItem,
            ["Покупатель получил не те товары"] = MarketplaceLossReason.WrongItem,
            ["Покупатель отказался при вручении: неполная комплектация"] = MarketplaceLossReason.Incomplete,
            ["Товар в неполной комплектации"] = MarketplaceLossReason.Incomplete,
            ["Неправильно указаны ОВХ товара"] = MarketplaceLossReason.CardMismatch,
            ["Товар поддельный"] = MarketplaceLossReason.Counterfeit,

            ["Не удалось доставить заказ"] = MarketplaceLossReason.DeliveryFailed,
            ["Покупатель отменил заказ: перенос сроков доставки"] = MarketplaceLossReason.DeliveryDelayed,
            ["Упаковка и товар повреждены"] = MarketplaceLossReason.DamagedInDelivery,
            ["Не удалось обработать заказ"] = MarketplaceLossReason.MarketplaceFailure,
            ["Не удалось зарегистрировать отправление в службе доставки"] = MarketplaceLossReason.MarketplaceFailure,
        };

    /// <summary>Null for no reason, <see cref="MarketplaceLossReason.Unknown"/> for an unrecognized one.</summary>
    public static MarketplaceLossReason? CategoryOf(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? null
            : Reasons.GetValueOrDefault(reason.Trim(), MarketplaceLossReason.Unknown);
}
