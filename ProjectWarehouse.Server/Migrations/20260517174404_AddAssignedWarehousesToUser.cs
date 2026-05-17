using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedWarehousesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationUserWarehouse",
                columns: table => new
                {
                    AssignedUsersId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedWarehousesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUserWarehouse", x => new { x.AssignedUsersId, x.AssignedWarehousesId });
                    table.ForeignKey(
                        name: "FK_ApplicationUserWarehouse_AspNetUsers_AssignedUsersId",
                        column: x => x.AssignedUsersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationUserWarehouse_Warehouses_AssignedWarehousesId",
                        column: x => x.AssignedWarehousesId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserWarehouse_AssignedWarehousesId",
                table: "ApplicationUserWarehouse",
                column: "AssignedWarehousesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationUserWarehouse");
        }
    }
}
