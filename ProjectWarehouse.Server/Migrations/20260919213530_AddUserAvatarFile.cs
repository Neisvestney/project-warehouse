using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatarFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvatarFileId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AvatarFileId",
                table: "AspNetUsers",
                column: "AvatarFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_DataFiles_AvatarFileId",
                table: "AspNetUsers",
                column: "AvatarFileId",
                principalTable: "DataFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_DataFiles_AvatarFileId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AvatarFileId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarFileId",
                table: "AspNetUsers");
        }
    }
}
