using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class SupervisorComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeFinalComment",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorFinalRank",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorRankComment",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeFinalComment",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "SupervisorFinalRank",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "SupervisorRankComment",
                table: "Appraisals");
        }
    }
}
