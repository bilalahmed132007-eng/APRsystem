using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class ContractsEnhanced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EmployeeId",
                table: "Contracts",
                column: "EmployeeId");
        }
    }
}
