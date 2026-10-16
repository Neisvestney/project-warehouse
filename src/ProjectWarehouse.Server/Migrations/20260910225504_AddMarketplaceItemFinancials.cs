using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceItemFinancials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "OrderMarketplaceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionCurrencyCode",
                table: "OrderMarketplaceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "OrderMarketplaceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCurrencyCode",
                table: "OrderMarketplaceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerPrice",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OldPrice",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Payout",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "OrderMarketplaceItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderMarketplaceItems_CatalogItemId",
                table: "OrderMarketplaceItems",
                column: "CatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderMarketplaceItems_CatalogItems_CatalogItemId",
                table: "OrderMarketplaceItems",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Rows imported before the snapshot existed: the live mapping is the only origin on record.
            // Money stays null for them — Ozon never sent it.
            migrationBuilder.Sql("""
                UPDATE "OrderMarketplaceItems" i
                SET "CatalogItemId" = c."CatalogItemId"
                FROM "MarketplaceCards" c
                WHERE i."MarketplaceCardId" = c."Id" AND c."CatalogItemId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderMarketplaceItems_CatalogItems_CatalogItemId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderMarketplaceItems_CatalogItemId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CommissionCurrencyCode",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CustomerCurrencyCode",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "CustomerPrice",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "OldPrice",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "Payout",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "OrderMarketplaceItems");
        }
    }
}
