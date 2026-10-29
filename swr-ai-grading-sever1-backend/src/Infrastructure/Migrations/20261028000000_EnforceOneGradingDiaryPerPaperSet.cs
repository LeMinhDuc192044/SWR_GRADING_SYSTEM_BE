using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261028000000_EnforceOneGradingDiaryPerPaperSet")]
    public partial class EnforceOneGradingDiaryPerPaperSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grading_diary_paper_set_id",
                table: "grading_diary");

            migrationBuilder.CreateIndex(
                name: "IX_grading_diary_paper_set_id",
                table: "grading_diary",
                column: "paper_set_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grading_diary_paper_set_id",
                table: "grading_diary");

            migrationBuilder.CreateIndex(
                name: "IX_grading_diary_paper_set_id",
                table: "grading_diary",
                column: "paper_set_id");
        }
    }
}
