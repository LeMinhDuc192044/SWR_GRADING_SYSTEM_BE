using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "grading_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_id",
                table: "applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_applications_submission_id",
                table: "applications",
                column: "submission_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_applications_student_submission_submission_id",
                table: "applications",
                column: "submission_id",
                principalTable: "student_submission",
                principalColumn: "submission_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_student_submission_submission_id",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "IX_applications_submission_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "grading_sessions");

            migrationBuilder.DropColumn(
                name: "submission_id",
                table: "applications");
        }
    }
}
