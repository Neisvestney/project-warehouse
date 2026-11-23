using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeYandexMarketCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders" AS o
                SET "CancelReasonCategory" = CASE o."RawCancelReasonCode"
                    WHEN 'USER_NOT_PAID' THEN 6
                    WHEN 'REPLACING_ORDER' THEN 1
                END
                FROM "MarketplaceAccounts" AS a
                WHERE a."Id" = o."MarketplaceAccountId" AND a."Type" = 2
                    AND o."CancelReasonCategory" = 0
                    AND o."RawCancelReasonCode" IN ('USER_NOT_PAID', 'REPLACING_ORDER');

                UPDATE "MarketplaceAccruals" AS c
                SET "Category" = m.category
                FROM "MarketplaceAccounts" AS a,
                    (VALUES
                        ('Возврат премии/Денежная премия за участие в совместных акциях', 10),
                        ('Премия/Денежная премия за участие в совместных акциях', 10),
                        ('Возврат скидки за участие в совместных акциях/Скидка за лояльность', 2),
                        ('Оплата услуг Маркета/Скидка за лояльность', 2),
                        ('Оплата услуг Маркета/', 12)
                    ) AS m(raw_type, category)
                WHERE a."Id" = c."MarketplaceAccountId" AND a."Type" = 2
                    AND c."Category" = 13 AND c."RawTypeId" = m.raw_type;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Classification repairs preserve source data and do not need to be reversed.
        }
    }
}
