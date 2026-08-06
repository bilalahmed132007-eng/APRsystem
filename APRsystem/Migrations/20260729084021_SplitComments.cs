using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class SplitComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SpecificComment",
                table: "Appraisals",
                newName: "SupervisorSpecificComment");

            migrationBuilder.RenameColumn(
                name: "GeneralComment",
                table: "Appraisals",
                newName: "SupervisorGeneralComment");

            migrationBuilder.AddColumn<string>(
                name: "SelfGeneralComment",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfSpecificComment",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelfGeneralComment",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "SelfSpecificComment",
                table: "Appraisals");

            migrationBuilder.RenameColumn(
                name: "SupervisorSpecificComment",
                table: "Appraisals",
                newName: "SpecificComment");

            migrationBuilder.RenameColumn(
                name: "SupervisorGeneralComment",
                table: "Appraisals",
                newName: "GeneralComment");
        }
    }
}
