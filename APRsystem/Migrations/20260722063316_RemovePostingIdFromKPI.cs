using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class RemovePostingIdFromKPI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs");

            migrationBuilder.DropIndex(
                name: "IX_KPIs_PostingId",
                table: "KPIs");

            migrationBuilder.DropColumn(
                name: "PostingId",
                table: "KPIs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PostingId",
                table: "KPIs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KPIs_PostingId",
                table: "KPIs",
                column: "PostingId");

            migrationBuilder.AddForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs",
                column: "PostingId",
                principalTable: "Postings",
                principalColumn: "Id");
        }
    }
}
