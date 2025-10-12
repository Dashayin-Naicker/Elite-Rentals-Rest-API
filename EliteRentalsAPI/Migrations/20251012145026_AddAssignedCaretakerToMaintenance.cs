using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EliteRentalsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedCaretakerToMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedCaretakerId",
                table: "Maintenance",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_AssignedCaretakerId",
                table: "Maintenance",
                column: "AssignedCaretakerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Maintenance_Users_AssignedCaretakerId",
                table: "Maintenance",
                column: "AssignedCaretakerId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Maintenance_Users_AssignedCaretakerId",
                table: "Maintenance");

            migrationBuilder.DropIndex(
                name: "IX_Maintenance_AssignedCaretakerId",
                table: "Maintenance");

            migrationBuilder.DropColumn(
                name: "AssignedCaretakerId",
                table: "Maintenance");
        }
    }
}
