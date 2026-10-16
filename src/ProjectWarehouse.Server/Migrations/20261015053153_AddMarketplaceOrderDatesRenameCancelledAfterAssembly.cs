using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceOrderDatesRenameCancelledAfterAssembly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CancelledAfterShip",
                table: "MarketplaceOrders",
                newName: "CancelledAfterAssembly");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveringAt",
                table: "MarketplaceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryDateBegin",
                table: "MarketplaceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryDateEnd",
                table: "MarketplaceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShipmentDateWithoutDelay",
                table: "MarketplaceOrders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveringAt",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDateBegin",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryDateEnd",
                table: "MarketplaceOrders");

            migrationBuilder.DropColumn(
                name: "ShipmentDateWithoutDelay",
                table: "MarketplaceOrders");

            migrationBuilder.RenameColumn(
                name: "CancelledAfterAssembly",
                table: "MarketplaceOrders",
                newName: "CancelledAfterShip");
        }
    }
}
