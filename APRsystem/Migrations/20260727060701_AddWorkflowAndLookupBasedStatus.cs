using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowAndLookupBasedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Appraisals",
                newName: "StatusId");

            migrationBuilder.RenameColumn(
                name: "Stage",
                table: "AppraisalHistories",
                newName: "StageId");

            migrationBuilder.RenameColumn(
                name: "NextStage",
                table: "AppraisalHistories",
                newName: "NextStageId");

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Lookups",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStateId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextStateId = table.Column<int>(type: "int", nullable: false),
                    IsCommentMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CrudPermission = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Lookups_CurrentStateId",
                        column: x => x.CurrentStateId,
                        principalTable: "Lookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Workflows_Lookups_NextStateId",
                        column: x => x.NextStateId,
                        principalTable: "Lookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_StatusId",
                table: "Appraisals",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHistories_NextStageId",
                table: "AppraisalHistories",
                column: "NextStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalHistories_StageId",
                table: "AppraisalHistories",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_CurrentStateId",
                table: "Workflows",
                column: "CurrentStateId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_NextStateId",
                table: "Workflows",
                column: "NextStateId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Appraisals_Lookups_StatusId",
                table: "Appraisals",
                column: "StatusId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.DropForeignKey(
                name: "FK_Appraisals_Lookups_StatusId",
                table: "Appraisals");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Appraisals_StatusId",
                table: "Appraisals");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalHistories_NextStageId",
                table: "AppraisalHistories");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalHistories_StageId",
                table: "AppraisalHistories");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Lookups");

            migrationBuilder.RenameColumn(
                name: "StatusId",
                table: "Appraisals",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "StageId",
                table: "AppraisalHistories",
                newName: "Stage");

            migrationBuilder.RenameColumn(
                name: "NextStageId",
                table: "AppraisalHistories",
                newName: "NextStage");
        }
    }
}
