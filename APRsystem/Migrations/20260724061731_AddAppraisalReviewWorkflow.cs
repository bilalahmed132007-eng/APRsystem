using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APRsystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedOn",
                table: "Appraisals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerComments",
                table: "Appraisals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewerId",
                table: "Appraisals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Appraisals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_ReviewerId",
                table: "Appraisals",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appraisals_Employees_ReviewerId",
                table: "Appraisals",
                column: "ReviewerId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appraisals_Employees_ReviewerId",
                table: "Appraisals");

            migrationBuilder.DropIndex(
                name: "IX_Appraisals_ReviewerId",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "ReviewedOn",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "ReviewerComments",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "Appraisals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Appraisals");
        }
    }
}
