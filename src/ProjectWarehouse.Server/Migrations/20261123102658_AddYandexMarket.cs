using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddYandexMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalItemId",
                table: "OrderMarketplaceItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawRefundStatus",
                table: "MarketplaceReturns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccrualsPausedUntil",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExternalBusinessId",
                table: "MarketplaceAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExternalCampaignId",
                table: "MarketplaceAccounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccounts_Type_ExternalCampaignId",
                table: "MarketplaceAccounts",
                columns: new[] { "Type", "ExternalCampaignId" },
                unique: true,
                filter: "\"ExternalCampaignId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceAccounts_Type_ExternalCampaignId",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalItemId",
                table: "OrderMarketplaceItems");

            migrationBuilder.DropColumn(
                name: "RawRefundStatus",
                table: "MarketplaceReturns");

            migrationBuilder.DropColumn(
                name: "AccrualsPausedUntil",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalBusinessId",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalCampaignId",
                table: "MarketplaceAccounts");
        }
    }
}
