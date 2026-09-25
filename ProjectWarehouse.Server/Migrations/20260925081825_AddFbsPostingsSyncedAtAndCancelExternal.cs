using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddFbsPostingsSyncedAtAndCancelExternal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FbsPostingsSyncedAt",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);

            // External orders whose goods never left the seller were imported as Shipped; Cancelled = 4, Canceled = 5
            migrationBuilder.Sql("""
                UPDATE "Orders" o
                SET "Status" = 5, "ShippedAt" = NULL
                FROM "MarketplaceOrders" m
                WHERE m."OrderId" = o."Id"
                  AND o."IsExternal"
                  AND o."Status" = 4
                  AND m."Status" = 4
                  AND m."CancelledAfterShip" IS DISTINCT FROM TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Orders" o
                SET "Status" = 4, "ShippedAt" = COALESCE(m."InProcessAt", o."CreatedAt")
                FROM "MarketplaceOrders" m
                WHERE m."OrderId" = o."Id"
                  AND o."IsExternal"
                  AND o."Status" = 5;
                """);

            migrationBuilder.DropColumn(
                name: "FbsPostingsSyncedAt",
                table: "MarketplaceAccounts");
        }
    }
}
