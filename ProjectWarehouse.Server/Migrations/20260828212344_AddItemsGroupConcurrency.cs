using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddItemsGroupConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup");

            // The unique index below cannot be created while duplicates exist: collapse each
            // (node, item) pair onto one row, carrying the stock of the rows that go. Id is a random
            // Guid and the table has no timestamp, so the survivor is simply the lowest Id — which of
            // the duplicates wins does not matter, nothing references these rows.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           first_value("Id") OVER w AS keep_id,
                           sum("Count") OVER w AS total
                    FROM "ItemsGroup"
                    WHERE "StoragePlaceNodeId" IS NOT NULL
                    WINDOW w AS (
                        PARTITION BY "StoragePlaceNodeId", "CatalogItemId"
                        ORDER BY "Id"
                        ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
                    )
                )
                UPDATE "ItemsGroup" g
                SET "Count" = r.total
                FROM ranked r
                WHERE g."Id" = r."Id" AND g."Id" = r.keep_id AND g."Count" <> r.total;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "ItemsGroup" g
                USING "ItemsGroup" k
                WHERE g."StoragePlaceNodeId" IS NOT NULL
                  AND k."StoragePlaceNodeId" = g."StoragePlaceNodeId"
                  AND k."CatalogItemId" = g."CatalogItemId"
                  AND k."Id" < g."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeId_CatalogItemId",
                table: "ItemsGroup",
                columns: new[] { "StoragePlaceNodeId", "CatalogItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeId_CatalogItemId",
                table: "ItemsGroup");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_StoragePlaceNodeId",
                table: "ItemsGroup",
                column: "StoragePlaceNodeId");
        }
    }
}
