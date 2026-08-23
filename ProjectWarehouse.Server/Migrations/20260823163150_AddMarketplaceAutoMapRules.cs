using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceAutoMapRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceAutoMapRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceAutoMapRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceAutoMapRules_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAutoMapRules_CatalogItemId",
                table: "MarketplaceAutoMapRules",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAutoMapRules_IsEnabled_Priority",
                table: "MarketplaceAutoMapRules",
                columns: new[] { "IsEnabled", "Priority" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceAutoMapRules");
        }
    }
}
