using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyoutsPausedUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BuyoutsPausedUntil",
                table: "MarketplaceAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyoutsPausedUntil",
                table: "MarketplaceAccounts");
        }
    }
}
