using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class PreserveWildberriesReturnAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RequestedOn",
                table: "MarketplaceReturns",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                table: "MarketplaceAccruals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MarketplaceAccounts" a
                SET "ReturnsSyncedAt" = LEAST(
                    a."ReturnsSyncedAt",
                    (SELECT MIN(c."Date")::timestamp AT TIME ZONE 'Europe/Moscow'
                     FROM "MarketplaceAccruals" c WHERE c."MarketplaceAccountId" = a."Id"),
                    (SELECT MIN(r."ReturnedAt") - INTERVAL '6 months'
                     FROM "MarketplaceReturns" r WHERE r."MarketplaceAccountId" = a."Id"),
                    CURRENT_TIMESTAMP - INTERVAL '90 days')
                WHERE a."Type" = 1;

                INSERT INTO "MarketplaceReturns"
                SELECT (jsonb_populate_record(NULL::"MarketplaceReturns", to_jsonb(r) || jsonb_build_object(
                    'Id', gen_random_uuid(),
                    'ExternalId', 'wb-seller-return:' || r."SourceExternalId",
                    'PostingNumber', '', 'OrderId', NULL, 'OrderMarketplaceItemId', NULL, 'CatalogItemId', NULL,
                    'Scheme', 0, 'Kind', 2, 'SaleReversedOn', NULL, 'Price', NULL,
                    'IsCancelled', COALESCE(r."RawStatus" LIKE 'Отмена%', FALSE)))).*
                FROM "MarketplaceReturns" r
                JOIN "MarketplaceAccounts" a ON a."Id" = r."MarketplaceAccountId"
                WHERE a."Type" = 1 AND r."SaleReversedOn" IS NOT NULL AND r."SourceExternalId" IS NOT NULL
                ON CONFLICT ("MarketplaceAccountId", "ExternalId") DO NOTHING;

                UPDATE "MarketplaceReturns" r
                SET "SourceExternalId" = NULL, "RawKind" = NULL, "RawStatus" = NULL, "StatusName" = NULL,
                    "ReturnedAt" = r."SaleReversedOn"::timestamp AT TIME ZONE 'Europe/Moscow',
                    "StatusChangedAt" = r."SaleReversedOn"::timestamp AT TIME ZONE 'Europe/Moscow',
                    "FinalAt" = NULL, "IsCancelled" = FALSE, "Kind" = 4
                FROM "MarketplaceAccounts" a
                WHERE a."Id" = r."MarketplaceAccountId" AND a."Type" = 1
                    AND r."SaleReversedOn" IS NOT NULL AND r."SourceExternalId" IS NOT NULL;

                UPDATE "MarketplaceReturns" r
                SET "ExternalId" = 'wb-seller-return:' || r."SourceExternalId",
                    "PostingNumber" = '', "OrderId" = NULL, "OrderMarketplaceItemId" = NULL,
                    "CatalogItemId" = NULL, "Scheme" = 0, "Kind" = 2
                FROM "MarketplaceAccounts" a
                WHERE a."Id" = r."MarketplaceAccountId" AND a."Type" = 1
                    AND r."SaleReversedOn" IS NULL AND r."SourceExternalId" IS NOT NULL
                    AND r."ExternalId" NOT LIKE 'wb-seller-return:%'
                    AND NOT EXISTS (SELECT 1 FROM "MarketplaceReturns" other
                        WHERE other."MarketplaceAccountId" = r."MarketplaceAccountId"
                            AND other."ExternalId" = 'wb-seller-return:' || r."SourceExternalId");

                UPDATE "MarketplaceAccruals" c
                SET "OrderId" = NULL, "OrderMarketplaceItemId" = NULL, "CatalogItemId" = NULL
                FROM "MarketplaceAccounts" a
                WHERE a."Id" = c."MarketplaceAccountId" AND a."Type" = 1
                    AND c."Rid" IN (SELECT r."SourceExternalId" FROM "MarketplaceReturns" r
                        WHERE r."MarketplaceAccountId" = a."Id"
                            AND r."ExternalId" LIKE 'wb-seller-return:%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedOn",
                table: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "MarketplaceAccruals");
        }
    }
}
