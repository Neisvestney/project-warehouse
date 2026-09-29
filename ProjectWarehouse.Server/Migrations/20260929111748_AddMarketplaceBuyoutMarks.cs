using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ProjectWarehouse.Server.Models;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceBuyoutMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<AppFieldError>(
                name: "Warning",
                table: "MarketplaceSyncRuns",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BuyoutsLoadedFrom",
                table: "MarketplaceAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BuyoutsSyncedAt",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Warning",
                table: "MarketplaceSyncRuns");

            migrationBuilder.DropColumn(
                name: "BuyoutsLoadedFrom",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "BuyoutsSyncedAt",
                table: "MarketplaceAccounts");
        }
    }
}
