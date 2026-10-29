using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceReturnSourceExternalIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceReturns_MarketplaceAccountId_SourceExternalId",
                table: "MarketplaceReturns",
                columns: new[] { "MarketplaceAccountId", "SourceExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceReturns_MarketplaceAccountId_SourceExternalId",
                table: "MarketplaceReturns");
        }
    }
}
