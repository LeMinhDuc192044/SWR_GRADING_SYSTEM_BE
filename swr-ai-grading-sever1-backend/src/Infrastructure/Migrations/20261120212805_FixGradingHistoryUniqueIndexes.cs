using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixGradingHistoryUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_grading_histories_submission_id_evaluation_type",
                table: "submission_grading_histories");

            migrationBuilder.CreateIndex(
                name: "ux_submission_grading_histories_one_official",
                table: "submission_grading_histories",
                column: "submission_id",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "ux_submission_grading_histories_submission_round",
                table: "submission_grading_histories",
                columns: new[] { "submission_id", "grading_round" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_submission_grading_histories_one_official",
                table: "submission_grading_histories");

            migrationBuilder.DropIndex(
                name: "ux_submission_grading_histories_submission_round",
                table: "submission_grading_histories");

            migrationBuilder.CreateIndex(
                name: "IX_submission_grading_histories_submission_id_evaluation_type",
                table: "submission_grading_histories",
                columns: new[] { "submission_id", "evaluation_type" },
                unique: true);
        }
    }
}
