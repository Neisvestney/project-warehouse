using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class SplitSyncRunQueuedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceSyncRuns_MarketplaceAccountId_StartedAt",
                table: "MarketplaceSyncRuns");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAt",
                table: "MarketplaceSyncRuns",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "MarketplaceSyncRuns",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // existing runs never recorded a separate start, so both dates take the old StartedAt
            migrationBuilder.Sql("""UPDATE "MarketplaceSyncRuns" SET "QueuedAt" = "StartedAt";""");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSyncRuns_MarketplaceAccountId_QueuedAt",
                table: "MarketplaceSyncRuns",
                columns: new[] { "MarketplaceAccountId", "QueuedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketplaceSyncRuns_MarketplaceAccountId_QueuedAt",
                table: "MarketplaceSyncRuns");

            migrationBuilder.Sql("""UPDATE "MarketplaceSyncRuns" SET "StartedAt" = "QueuedAt" WHERE "StartedAt" IS NULL;""");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "MarketplaceSyncRuns");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAt",
                table: "MarketplaceSyncRuns",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceSyncRuns_MarketplaceAccountId_StartedAt",
                table: "MarketplaceSyncRuns",
                columns: new[] { "MarketplaceAccountId", "StartedAt" },
                descending: new[] { false, true });
        }
    }
}
