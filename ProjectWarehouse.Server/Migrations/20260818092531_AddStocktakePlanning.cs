using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakePlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedDate",
                table: "Stocktakes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Stocktakes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedDate",
                table: "Stocktakes");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Stocktakes");
        }
    }
}
