using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class StoreCleanMarketplaceLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabelKind",
                table: "MarketplaceOrders",
                type: "integer",
                nullable: true);

            // Stored labels already carry stamped articles; drop them so every label is fetched clean. The files
            // become orphans for the data file GC.
            migrationBuilder.Sql(
                """UPDATE "MarketplaceOrders" SET "LabelFileId" = NULL, "LabelFetchedAt" = NULL WHERE "LabelFileId" IS NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelKind",
                table: "MarketplaceOrders");
        }
    }
}
