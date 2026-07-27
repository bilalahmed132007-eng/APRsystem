using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSection6And7Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionRequired",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalRank",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HRRemarks",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationText",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedRank",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionRequired",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "FinalRank",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "HRRemarks",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "RecommendationText",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "RecommendedRank",
                table: "Appraisals");
        }
    }
}
