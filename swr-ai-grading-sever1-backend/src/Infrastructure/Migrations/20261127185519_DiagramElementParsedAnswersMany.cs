using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiagramElementParsedAnswersMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_parsed_answers_diagram_elements_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.DropIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers",
                column: "element_id");

            migrationBuilder.AddForeignKey(
                name: "FK_submission_parsed_answers_diagram_elements_element_id",
                table: "submission_parsed_answers",
                column: "element_id",
                principalTable: "diagram_elements",
                principalColumn: "element_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_submission_parsed_answers_diagram_elements_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.DropIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers");

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers",
                column: "element_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_submission_parsed_answers_diagram_elements_element_id",
                table: "submission_parsed_answers",
                column: "element_id",
                principalTable: "diagram_elements",
                principalColumn: "element_id");
        }
    }
}
