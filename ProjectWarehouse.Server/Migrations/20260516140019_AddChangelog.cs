using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using ProjectWarehouse.Server.Domain;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChangelog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeLogEntryType = table.Column<int>(type: "integer", nullable: false),
                    Diffs = table.Column<IList<ChangeLogDiff>>(type: "jsonb", nullable: false),
                    Snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    Context = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: true),
                    ActionData = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeLogEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_EntityId",
                table: "ChangeLogEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_EntityType",
                table: "ChangeLogEntries",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_UserId",
                table: "ChangeLogEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeLogEntries");
        }
    }
}
