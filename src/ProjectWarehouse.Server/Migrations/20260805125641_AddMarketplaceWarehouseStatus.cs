using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceWarehouseStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "MarketplaceWarehouses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill from the only provider that exists so far — without it every row reads
            // as Unavailable until the next sync overwrites it.
            migrationBuilder.Sql("""
                UPDATE "MarketplaceWarehouses" SET "Status" = 1 WHERE "ExternalStatus" = 'created';
                UPDATE "MarketplaceWarehouses" SET "Status" = 2 WHERE "ExternalStatus" = 'disabled';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "MarketplaceWarehouses");
        }
    }
}
