using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class RestructurePostingKPI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostingKPIs_KPIs_KPIId",
                table: "PostingKPIs");

            migrationBuilder.DropIndex(
                name: "IX_PostingKPIs_KPIId",
                table: "PostingKPIs");

            migrationBuilder.DropColumn(
                name: "KPIId",
                table: "PostingKPIs");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PostingKPIs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PostingKPIs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PostingKPIs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "PostingKPIs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "PostingKPIs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PostingKPIs");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "PostingKPIs");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "PostingKPIs");

            migrationBuilder.AddColumn<int>(
                name: "KPIId",
                table: "PostingKPIs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PostingKPIs_KPIId",
                table: "PostingKPIs",
                column: "KPIId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostingKPIs_KPIs_KPIId",
                table: "PostingKPIs",
                column: "KPIId",
                principalTable: "KPIs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
