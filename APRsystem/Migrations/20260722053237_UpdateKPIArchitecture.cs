using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKPIArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs");

            migrationBuilder.AlterColumn<int>(
                name: "PostingId",
                table: "KPIs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsGeneral",
                table: "KPIs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs",
                column: "PostingId",
                principalTable: "Postings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs");

            migrationBuilder.DropColumn(
                name: "IsGeneral",
                table: "KPIs");

            migrationBuilder.AlterColumn<int>(
                name: "PostingId",
                table: "KPIs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KPIs_Postings_PostingId",
                table: "KPIs",
                column: "PostingId",
                principalTable: "Postings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
