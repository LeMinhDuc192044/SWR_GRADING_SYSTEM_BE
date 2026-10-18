using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers",
                column: "element_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers",
                column: "element_id");
        }
    }
}
