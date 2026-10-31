using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegradeApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regrade_applications",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assigned_lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    original_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    regraded_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    lecturer_feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regrade_applications", x => x.application_id);
                    table.ForeignKey(
                        name: "FK_regrade_applications_lecturers_assigned_lecturer_id",
                        column: x => x.assigned_lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_regrade_applications_student_submission_submission_id",
                        column: x => x.submission_id,
                        principalTable: "student_submission",
                        principalColumn: "submission_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_regrade_applications_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regrade_applications_users_admin_id",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_regrade_applications_admin_id",
                table: "regrade_applications",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_regrade_applications_assigned_lecturer_id",
                table: "regrade_applications",
                column: "assigned_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_regrade_applications_student_id",
                table: "regrade_applications",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_regrade_applications_submission_id",
                table: "regrade_applications",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regrade_applications");
        }
    }
}
