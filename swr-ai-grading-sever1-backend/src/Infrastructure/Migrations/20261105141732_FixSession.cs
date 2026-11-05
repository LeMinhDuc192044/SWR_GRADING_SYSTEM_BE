using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_examination_sessions_students_StudentId1",
                table: "student_examination_sessions");

            migrationBuilder.DropIndex(
                name: "IX_student_examination_sessions_StudentId1",
                table: "student_examination_sessions");

            migrationBuilder.DropColumn(
                name: "StudentId1",
                table: "student_examination_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentId1",
                table: "student_examination_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_examination_sessions_StudentId1",
                table: "student_examination_sessions",
                column: "StudentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_student_examination_sessions_students_StudentId1",
                table: "student_examination_sessions",
                column: "StudentId1",
                principalTable: "students",
                principalColumn: "id");
        }
    }
}
