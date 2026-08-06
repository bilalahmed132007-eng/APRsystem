using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfrating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalHistories_Lookups_NextStageId",
                table: "AppraisalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalHistories_Lookups_StageId",
                table: "AppraisalHistories");

            migrationBuilder.AddColumn<int>(
                name: "SelfRating",
                table: "AppraisalKPIs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SelfScore",
                table: "AppraisalKPIs",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalHistories_Lookups_NextStageId",
                table: "AppraisalHistories",
                column: "NextStageId",
                principalTable: "Lookups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalHistories_Lookups_StageId",
                table: "AppraisalHistories",
                column: "StageId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalHistories_Lookups_NextStageId",
                table: "AppraisalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalHistories_Lookups_StageId",
                table: "AppraisalHistories");

            migrationBuilder.DropColumn(
                name: "SelfRating",
                table: "AppraisalKPIs");

            migrationBuilder.DropColumn(
                name: "SelfScore",
                table: "AppraisalKPIs");

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalHistories_Lookups_NextStageId",
                table: "AppraisalHistories",
                column: "NextStageId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalHistories_Lookups_StageId",
                table: "AppraisalHistories",
                column: "StageId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
