using System;
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
            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.semester_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    birthday = table.Column<DateOnly>(type: "date", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    cccd = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "examinations",
                columns: table => new
                {
                    examination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_code = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    examination_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    before_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_examinations", x => x.examination_id);
                    table.ForeignKey(
                        name: "FK_examinations_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "semester_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lecturers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturers", x => x.id);
                    table.ForeignKey(
                        name: "FK_lecturers_users_id",
                        column: x => x.id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    major = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.id);
                    table.ForeignKey(
                        name: "FK_students_users_id",
                        column: x => x.id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_sets",
                columns: table => new
                {
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paper_set_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    file_question_docs = table.Column<string>(type: "text", nullable: true),
                    file_answer_rubric = table.Column<string>(type: "text", nullable: true),
                    file_answer_template = table.Column<string>(type: "text", nullable: true),
                    file_examination_type = table.Column<int>(type: "integer", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    examination_id = table.Column<Guid>(type: "uuid", nullable: true),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_sets", x => x.paper_set_id);
                    table.ForeignKey(
                        name: "FK_paper_sets_examinations_examination_id",
                        column: x => x.examination_id,
                        principalTable: "examinations",
                        principalColumn: "examination_id");
                    table.ForeignKey(
                        name: "FK_paper_sets_lecturers_create_by_id",
                        column: x => x.create_by_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_paper_sets_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "semester_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_examination",
                columns: table => new
                {
                    student_examination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_examination", x => x.student_examination_id);
                    table.ForeignKey(
                        name: "FK_student_examination_examinations_exam_id",
                        column: x => x.exam_id,
                        principalTable: "examinations",
                        principalColumn: "examination_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_examination_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grading_diary",
                columns: table => new
                {
                    grading_diary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_code = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    create_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_diary", x => x.grading_diary_id);
                    table.ForeignKey(
                        name: "FK_grading_diary_lecturers_create_by_id",
                        column: x => x.create_by_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grading_diary_paper_sets_paper_set_id",
                        column: x => x.paper_set_id,
                        principalTable: "paper_sets",
                        principalColumn: "paper_set_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    point = table.Column<decimal>(type: "numeric", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.question_id);
                    table.ForeignKey(
                        name: "FK_questions_paper_sets_paper_set_id",
                        column: x => x.paper_set_id,
                        principalTable: "paper_sets",
                        principalColumn: "paper_set_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_submission",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ai_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    lecturer_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ai_logs = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    diary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_examination_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_submission", x => x.submission_id);
                    table.ForeignKey(
                        name: "FK_student_submission_grading_diary_diary_id",
                        column: x => x.diary_id,
                        principalTable: "grading_diary",
                        principalColumn: "grading_diary_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_submission_student_examination_student_examination_~",
                        column: x => x.student_examination_id,
                        principalTable: "student_examination",
                        principalColumn: "student_examination_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_examinations_examination_code",
                table: "examinations",
                column: "examination_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_examinations_semester_id",
                table: "examinations",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_grading_diary_create_by_id",
                table: "grading_diary",
                column: "create_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_grading_diary_paper_set_id",
                table: "grading_diary",
                column: "paper_set_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_create_by_id",
                table: "paper_sets",
                column: "create_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_examination_id",
                table: "paper_sets",
                column: "examination_id");

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_semester_id",
                table: "paper_sets",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_paper_set_id",
                table: "questions",
                column: "paper_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_semesters_semester_code",
                table: "semesters",
                column: "semester_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_examination_exam_id",
                table: "student_examination",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_examination_student_id",
                table: "student_examination",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_diary_id",
                table: "student_submission",
                column: "diary_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_student_examination_id",
                table: "student_submission",
                column: "student_examination_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "student_submission");

            migrationBuilder.DropTable(
                name: "grading_diary");

            migrationBuilder.DropTable(
                name: "student_examination");

            migrationBuilder.DropTable(
                name: "paper_sets");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "examinations");

            migrationBuilder.DropTable(
                name: "lecturers");

            migrationBuilder.DropTable(
                name: "semesters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
