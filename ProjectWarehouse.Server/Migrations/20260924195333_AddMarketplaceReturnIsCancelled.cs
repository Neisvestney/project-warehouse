using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceReturnIsCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "MarketplaceReturns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // same set as OzonClient.CancelledReturnStatuses; the stream never re-reads an unchanged return, so only this fills existing rows
            migrationBuilder.Sql("""
                UPDATE "MarketplaceReturns"
                SET "IsCancelled" = TRUE
                WHERE lower("RawStatus") IN ('cancelled', 'rejected', 'crmrejected', 'cancelleddisputenotopen');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "MarketplaceReturns");
        }
    }
}
