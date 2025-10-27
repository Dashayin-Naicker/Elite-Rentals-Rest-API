using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRentalsAPI.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveFunctionality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "Leases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Leases",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Leases");
        }
    }
}
