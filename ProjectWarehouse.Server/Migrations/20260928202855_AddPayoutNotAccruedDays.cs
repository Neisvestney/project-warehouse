using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutNotAccruedDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayoutNotAccruedDays",
                table: "AnalyticsSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AnalyticsSettings",
                keyColumn: "Id",
                keyValue: new Guid("5b0f7c1e-3a44-4f7e-9d7a-2c1e8a6b9f01"),
                column: "PayoutNotAccruedDays",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutNotAccruedDays",
                table: "AnalyticsSettings");
        }
    }
}
