using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeparateMarketplaceCancellationNoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both WB FBO report paths omit the initiator; FBS and other providers are not inferred here.
            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders" AS m
                SET "CancellationType" = 6
                FROM "MarketplaceAccounts" AS a, "Orders" AS o
                WHERE a."Id" = m."MarketplaceAccountId"
                  AND a."Type" = 1
                  AND o."Id" = m."OrderId"
                  AND o."Type" = 3
                  AND o."IsExternal" = TRUE
                  AND m."Status" = 4
                  AND m."CancellationType" = 0
                  AND m."RawCancellationType" IS NULL
                  AND m."RawStatus" IS NULL
                  AND m."CancelledAfterAssembly" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MarketplaceOrders"
                SET "CancellationType" = 0
                WHERE "CancellationType" = 6;
                """);
        }
    }
}
