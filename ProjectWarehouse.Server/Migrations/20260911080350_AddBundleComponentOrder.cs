using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBundleComponentOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BundleComponents_BundleId",
                table: "BundleComponents");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "BundleComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "BundleComponents" bc
                SET "Order" = ranked.rn
                FROM (
                    SELECT b."Id",
                           ROW_NUMBER() OVER (PARTITION BY b."BundleId" ORDER BY c."Name", b."Id") - 1 AS rn
                    FROM "BundleComponents" b
                    JOIN "CatalogItems" c ON c."Id" = b."ComponentId"
                ) AS ranked
                WHERE ranked."Id" = bc."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BundleComponents_BundleId_Order",
                table: "BundleComponents",
                columns: new[] { "BundleId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BundleComponents_BundleId_Order",
                table: "BundleComponents");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "BundleComponents");

            migrationBuilder.CreateIndex(
                name: "IX_BundleComponents_BundleId",
                table: "BundleComponents",
                column: "BundleId");
        }
    }
}
