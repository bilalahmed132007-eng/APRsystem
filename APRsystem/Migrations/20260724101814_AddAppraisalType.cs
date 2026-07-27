using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Appraisals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Appraisals");
        }
    }
}
