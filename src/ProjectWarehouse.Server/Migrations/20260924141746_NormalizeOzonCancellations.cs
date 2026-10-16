using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeOzonCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Final-status postings are never re-polled, so rows written by the old normalization stay as they
            // are unless rewritten here from the raw values kept alongside.
            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders"
                SET "CancelledAfterShip" = NULL, "CancellationType" = NULL
                WHERE "CancelledAfterShip" = FALSE
                  AND "RawCancellationType" IS NULL
                  AND "CancelReason" IS NULL
                  AND "MarketplaceAccountId" IN (SELECT "Id" FROM "MarketplaceAccounts" WHERE "Type" = 0);
                """);

            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders"
                SET "CancellationType" = CASE lower("RawCancellationType")
                    WHEN 'seller' THEN 1
                    WHEN 'client' THEN 2
                    WHEN 'customer' THEN 2
                    WHEN 'ozon' THEN 3
                    WHEN 'system' THEN 4
                    WHEN 'delivery' THEN 5
                    ELSE 0
                END
                WHERE "RawCancellationType" IS NOT NULL
                  AND "MarketplaceAccountId" IN (SELECT "Id" FROM "MarketplaceAccounts" WHERE "Type" = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
