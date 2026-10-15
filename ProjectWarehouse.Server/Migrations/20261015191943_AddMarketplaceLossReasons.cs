using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceLossReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawReasonCode",
                table: "MarketplaceReturns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasonCategory",
                table: "MarketplaceReturns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelReasonCategory",
                table: "MarketplaceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawCancelReasonCode",
                table: "MarketplaceOrders",
                type: "text",
                nullable: true);

            // Cancelled postings are never re-polled, so their category comes only from here. A snapshot of
            // OzonLossReasons as of this migration; a wording missing from it lands as Unknown (0).
            const string ozonReasons = """
                WITH reasons(reason, category) AS (VALUES
                    ('Покупатель отменил заказ', 1),
                    ('Покупатель передумал', 1),
                    ('Покупатель отменил заказ: нашел дешевле', 2),
                    ('Покупатель отменил заказ: не устроил срок доставки', 3),
                    ('Покупатель отказался при вручении: товар не подошел', 4),
                    ('Покупатель не забрал заказ', 5),
                    ('Покупатель не предоставил паспортные данные', 6),
                    ('Товар закончился на складе', 20),
                    ('Вы не отгрузили заказ вовремя', 21),
                    ('Вы отменили заказ', 22),
                    ('Товар не работает / брак', 23),
                    ('Товар сломался при эксплуатации', 23),
                    ('Товар поврежден, но упаковка цела', 23),
                    ('Покупатель отказался при вручении: недоволен качеством товара', 24),
                    ('Покупатель отказался при вручении: в заказе не тот товар', 25),
                    ('Покупатель получил не те товары', 25),
                    ('Покупатель отказался при вручении: неполная комплектация', 26),
                    ('Товар в неполной комплектации', 26),
                    ('Неправильно указаны ОВХ товара', 27),
                    ('Товар поддельный', 28),
                    ('Не удалось доставить заказ', 40),
                    ('Покупатель отменил заказ: перенос сроков доставки', 41),
                    ('Упаковка и товар повреждены', 42),
                    ('Не удалось обработать заказ', 43),
                    ('Не удалось зарегистрировать отправление в службе доставки', 43))
                """;

            migrationBuilder.Sql($"""
                {ozonReasons}
                UPDATE "MarketplaceOrders" o
                SET "CancelReasonCategory" = COALESCE(
                    (SELECT r.category FROM reasons r WHERE r.reason = trim(o."CancelReason")), 0)
                WHERE trim(o."CancelReason") <> ''
                  AND o."MarketplaceAccountId" IN (SELECT "Id" FROM "MarketplaceAccounts" WHERE "Type" = 0);
                """);

            migrationBuilder.Sql($"""
                {ozonReasons}
                UPDATE "MarketplaceReturns" t
                SET "ReasonCategory" = COALESCE(
                    (SELECT r.category FROM reasons r WHERE r.reason = trim(t."Reason")), 0)
                WHERE trim(t."Reason") <> ''
                  AND t."MarketplaceAccountId" IN (SELECT "Id" FROM "MarketplaceAccounts" WHERE "Type" = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawReasonCode",
                table: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "ReasonCategory",
                table: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "CancelReasonCategory",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "RawCancelReasonCode",
                table: "MarketplaceOrders");
        }
    }
}
