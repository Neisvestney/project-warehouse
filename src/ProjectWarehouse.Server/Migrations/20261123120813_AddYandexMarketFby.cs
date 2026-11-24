using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddYandexMarketFby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "YandexMarketModel",
                table: "MarketplaceAccounts",
                type: "integer",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "MarketplaceAccounts"
                SET "YandexMarketModel" = 0
                WHERE "Type" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YandexMarketModel",
                table: "MarketplaceAccounts");
        }
    }
}
