using System.Globalization;
using System.Text.Json;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.YandexMarket;

public static class YandexMarketAccrualParser
{
    private sealed record Row(DateOnly Date, string? OrderId, string? Sku, string RawType,
        MarketplaceAccrualCategory Category, decimal Amount);

    public static async Task<IReadOnlyList<ExternalAccrual>> ParseAsync(Stream stream, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException("Yandex Market report sheet exceeds the configured size limit.");
            buffer.Write(chunk, 0, read);
        }
        using var document = JsonDocument.Parse(buffer.ToArray());
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rows", out var reportRows))
            root = reportRows;
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Yandex Market transaction sheet must contain an array of rows.");
        var rows = new List<Row>();
        string? inn = null;
        foreach (var value in root.EnumerateArray())
        {
            var sellerInn = Text(value, "inn");
            if (!string.IsNullOrWhiteSpace(sellerInn))
            {
                if (inn is not null && !string.Equals(inn, sellerInn, StringComparison.Ordinal))
                    throw new InvalidDataException("Yandex Market report contains multiple seller INNs.");
                inn = sellerInn;
            }
            var type = Required(value, "transactionType");
            var source = Required(value, "transactionSource");
            if (source == "Внесено продавцом") continue;
            var date = DateTime.ParseExact(Required(value, "transactionDate"), "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
            var amountText = Required(value, "transactionSum");
            if (!decimal.TryParse(amountText, NumberStyles.Number | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture, out var amount))
                throw new InvalidDataException("Yandex Market transactionSum is not a decimal number.");
            var service = Text(value, "offerOrServiceName");
            var sale = source is "Платёж покупателя" or "Платеж покупателя" or "Баллы за скидку Маркета"
                or "Возврат платежа покупателя" or "Возврат баллов за скидку Маркета";
            var raw = sale ? source : $"{source}/{service}";
            var orderId = Text(value, "orderId");
            if (orderId is "" or "0") orderId = null;
            rows.Add(new(DateOnly.FromDateTime(date), orderId, Text(value, "shopSku"), raw,
                sale ? MarketplaceAccrualCategory.Sale : TransactionCategory(source, service), amount));
        }
        var result = rows.GroupBy(r => (r.Date, r.OrderId))
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.OrderId, StringComparer.Ordinal)
            .Select(g => new ExternalAccrual($"{g.Key.OrderId ?? "account"}:{g.Key.Date:yyyy-MM-dd}", g.Key.Date,
                g.Key.OrderId is null ? MarketplaceAccrualScope.Account : MarketplaceAccrualScope.Posting,
                g.Key.OrderId, g.OrderBy(r => r.RawType, StringComparer.Ordinal).ThenBy(r => r.Sku, StringComparer.Ordinal)
                    .ThenBy(r => r.Category).ThenBy(r => r.Amount)
                    .Select(r => new ExternalAccrualLine(r.Category, r.RawType, r.Sku, r.Amount, "RUB")).ToArray(),
                SellerInn: inn)).ToList();
        if (inn is not null && result.Count == 0)
            result.Add(new("metadata", default, MarketplaceAccrualScope.Account, null, [], SellerInn: inn, IsMetadata: true));
        return result;
    }

    private static string Required(JsonElement row, string name) => Text(row, name)
        ?? throw new InvalidDataException($"Yandex Market report is missing {name}.");

    private static string? Text(JsonElement row, string name) => !row.TryGetProperty(name, out var value)
        || value.ValueKind == JsonValueKind.Null ? null : value.ValueKind == JsonValueKind.String
        ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.GetRawText()
        : throw new InvalidDataException($"Yandex Market report field {name} has an invalid type.");

    public static MarketplaceAccrualCategory TransactionCategory(string source, string? service) => (source, service) switch
    {
        ("Оплата услуг Маркета", null or "") => MarketplaceAccrualCategory.Services,
        ("Премия" or "Возврат премии", "Денежная премия за участие в совместных акциях")
            => MarketplaceAccrualCategory.Compensation,
        _ => ServiceCategory(service),
    };

    public static MarketplaceAccrualCategory ServiceCategory(string? value) => value switch
    {
        "Размещение товарных предложений" or "Скидка за лояльность" => MarketplaceAccrualCategory.Commission,
        "Доставка покупателю" or "Доставка (средняя миля)" => MarketplaceAccrualCategory.Logistics,
        "Доставка невыкупов и возвратов" => MarketplaceAccrualCategory.ReturnLogistics,
        "Приём платежа" or "Прием платежа" or "Перевод платежа" => MarketplaceAccrualCategory.Acquiring,
        "Буст продаж, оплата за продажи" => MarketplaceAccrualCategory.Advertising,
        "Отгрузка или доставка не вовремя" => MarketplaceAccrualCategory.Penalty,
        _ => MarketplaceAccrualCategory.Other,
    };
}
