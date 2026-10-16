using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ProjectWarehouse.Server.Models;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalClientId = table.Column<string>(type: "text", nullable: true),
                    ApiKeyProtected = table.Column<string>(type: "text", nullable: false),
                    ApiKeyLast4 = table.Column<string>(type: "text", nullable: false),
                    ApiKeyUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncStatus = table.Column<int>(type: "integer", nullable: true),
                    LastSyncError = table.Column<AppFieldError>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceAccounts_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    OfferId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Barcodes = table.Column<string>(type: "jsonb", nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MappingSource = table.Column<int>(type: "integer", nullable: true),
                    MappedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceCards_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplaceCards_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehousesProcessed = table.Column<int>(type: "integer", nullable: false),
                    CardsProcessed = table.Column<int>(type: "integer", nullable: false),
                    CardsCreated = table.Column<int>(type: "integer", nullable: false),
                    CardsUpdated = table.Column<int>(type: "integer", nullable: false),
                    CardsArchived = table.Column<int>(type: "integer", nullable: false),
                    AutoMapped = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<AppFieldError>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceSyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceSyncRuns_AspNetUsers_TriggeredById",
                        column: x => x.TriggeredById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketplaceSyncRuns_MarketplaceAccounts_MarketplaceAccountId",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketplaceWarehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExternalStatus = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplaceWarehouses_MarketplaceAccounts_MarketplaceAccoun~",
                        column: x => x.MarketplaceAccountId,
                        principalTable: "MarketplaceAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketplaceWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccounts_CreatedById",
                table: "MarketplaceAccounts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAccounts_Type",
                table: "MarketplaceAccounts",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceCards_CatalogItemId",
                table: "MarketplaceCards",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceCards_MarketplaceAccountId_ExternalId",
                table: "MarketplaceCards",
                columns: new[] { "MarketplaceAccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceCards_MarketplaceAccountId_OfferId",
                table: "MarketplaceCards",
                columns: new[] { "MarketplaceAccountId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSyncRuns_MarketplaceAccountId_StartedAt",
                table: "MarketplaceSyncRuns",
                columns: new[] { "MarketplaceAccountId", "StartedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSyncRuns_TriggeredById",
                table: "MarketplaceSyncRuns",
                column: "TriggeredById");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceWarehouses_MarketplaceAccountId_ExternalId",
                table: "MarketplaceWarehouses",
                columns: new[] { "MarketplaceAccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceWarehouses_WarehouseId",
                table: "MarketplaceWarehouses",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceCards");

            migrationBuilder.DropTable(
                name: "MarketplaceSyncRuns");

            migrationBuilder.DropTable(
                name: "MarketplaceWarehouses");

            migrationBuilder.DropTable(
                name: "MarketplaceAccounts");
        }
    }
}
