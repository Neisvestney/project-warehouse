using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceSellerInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyLegalName",
                table: "MarketplaceAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inn",
                table: "MarketplaceAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ogrn",
                table: "MarketplaceAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipForm",
                table: "MarketplaceAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyLegalName",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "Inn",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "Ogrn",
                table: "MarketplaceAccounts");

            migrationBuilder.DropColumn(
                name: "OwnershipForm",
                table: "MarketplaceAccounts");
        }
    }
}
