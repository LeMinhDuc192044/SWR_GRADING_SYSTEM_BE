using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSemesterFromPaperSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_paper_sets_semesters_semester_id",
                table: "paper_sets");

            migrationBuilder.DropIndex(
                name: "IX_paper_sets_semester_id",
                table: "paper_sets");

            migrationBuilder.DropColumn(
                name: "semester_id",
                table: "paper_sets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "semester_id",
                table: "paper_sets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_semester_id",
                table: "paper_sets",
                column: "semester_id");

            migrationBuilder.AddForeignKey(
                name: "FK_paper_sets_semesters_semester_id",
                table: "paper_sets",
                column: "semester_id",
                principalTable: "semesters",
                principalColumn: "semester_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
