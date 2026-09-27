using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbcBoundaryA = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    AbcBoundaryB = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    XyzBoundaryX = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    XyzBoundaryY = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    XyzStep = table.Column<int>(type: "integer", nullable: true),
                    XyzMinIntervals = table.Column<int>(type: "integer", nullable: true),
                    PayoutRatioWindowDays = table.Column<int>(type: "integer", nullable: true),
                    PayoutAgeBoundaries = table.Column<int[]>(type: "integer[]", nullable: true),
                    PayoutOverdueDays = table.Column<int>(type: "integer", nullable: true),
                    ReturnsMaturityDays = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsSettings_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "AnalyticsSettings",
                columns: new[] { "Id", "AbcBoundaryA", "AbcBoundaryB", "PayoutAgeBoundaries", "PayoutOverdueDays", "PayoutRatioWindowDays", "ReturnsMaturityDays", "UpdatedAt", "UpdatedById", "XyzBoundaryX", "XyzBoundaryY", "XyzMinIntervals", "XyzStep" },
                values: new object[] { new Guid("5b0f7c1e-3a44-4f7e-9d7a-2c1e8a6b9f01"), null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSettings_UpdatedById",
                table: "AnalyticsSettings",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsSettings");
        }
    }
}
