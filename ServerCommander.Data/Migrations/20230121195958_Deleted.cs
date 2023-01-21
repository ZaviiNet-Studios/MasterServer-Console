using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerCommander.Data.Migrations
{
    /// <inheritdoc />
    public partial class Deleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateDeleted",
                table: "ServerInstances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromServer",
                table: "PlayerCountUpdates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateDeleted",
                table: "ServerInstances");

            migrationBuilder.DropColumn(
                name: "FromServer",
                table: "PlayerCountUpdates");
        }
    }
}
