using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfillmentResolvedItemAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AssemblyFulfillments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AssemblyFulfillments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedCatalogItemId",
                table: "AssemblyFulfillments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_CreatedById",
                table: "AssemblyFulfillments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_ResolvedCatalogItemId",
                table: "AssemblyFulfillments",
                column: "ResolvedCatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssemblyFulfillments_AspNetUsers_CreatedById",
                table: "AssemblyFulfillments",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssemblyFulfillments_CatalogItems_ResolvedCatalogItemId",
                table: "AssemblyFulfillments",
                column: "ResolvedCatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssemblyFulfillments_AspNetUsers_CreatedById",
                table: "AssemblyFulfillments");

            migrationBuilder.DropForeignKey(
                name: "FK_AssemblyFulfillments_CatalogItems_ResolvedCatalogItemId",
                table: "AssemblyFulfillments");

            migrationBuilder.DropIndex(
                name: "IX_AssemblyFulfillments_CreatedById",
                table: "AssemblyFulfillments");

            migrationBuilder.DropIndex(
                name: "IX_AssemblyFulfillments_ResolvedCatalogItemId",
                table: "AssemblyFulfillments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AssemblyFulfillments");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AssemblyFulfillments");

            migrationBuilder.DropColumn(
                name: "ResolvedCatalogItemId",
                table: "AssemblyFulfillments");
        }
    }
}
