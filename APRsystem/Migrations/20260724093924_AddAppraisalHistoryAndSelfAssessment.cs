using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalHistoryAndSelfAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SelfAssessmentEnabled",
                table: "Appraisals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppraisalHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppraisalId = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionByRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    NextStage = table.Column<int>(type: "int", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalHistories_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHistories_AppraisalId",
                table: "AppraisalHistories",
                column: "AppraisalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalHistories");

            migrationBuilder.DropColumn(
                name: "SelfAssessmentEnabled",
                table: "Appraisals");
        }
    }
}
