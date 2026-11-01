using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagrams",
                columns: table => new
                {
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    file_template_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagrams", x => x.diagram_id);
                });

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
                name: "diagram_elements",
                columns: table => new
                {
                    element_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    shape_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_requirement = table.Column<bool>(type: "boolean", nullable: false),
                    file_template_url = table.Column<string>(type: "text", nullable: true),
                    element_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    element_reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    software_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagram_elements", x => x.element_id);
                    table.ForeignKey(
                        name: "FK_diagram_elements_diagram_elements_element_reference_id",
                        column: x => x.element_reference_id,
                        principalTable: "diagram_elements",
                        principalColumn: "element_id");
                    table.ForeignKey(
                        name: "FK_diagram_elements_diagrams_diagram_id",
                        column: x => x.diagram_id,
                        principalTable: "diagrams",
                        principalColumn: "diagram_id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "student_semester",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_semester", x => new { x.student_id, x.semester_id });
                    table.ForeignKey(
                        name: "FK_student_semester_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "semester_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_semester_students_student_id",
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
                    examination_code = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: true),
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
                name: "question_rubrics",
                columns: table => new
                {
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    point = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_rubrics", x => x.rubric_id);
                    table.ForeignKey(
                        name: "FK_question_rubrics_diagrams_diagram_id",
                        column: x => x.diagram_id,
                        principalTable: "diagrams",
                        principalColumn: "diagram_id");
                    table.ForeignKey(
                        name: "FK_question_rubrics_paper_sets_paper_set_id",
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
                    file_submissions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_student_submission_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    point = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.question_id);
                    table.ForeignKey(
                        name: "FK_questions_diagrams_diagram_id",
                        column: x => x.diagram_id,
                        principalTable: "diagrams",
                        principalColumn: "diagram_id");
                    table.ForeignKey(
                        name: "FK_questions_paper_sets_paper_set_id",
                        column: x => x.paper_set_id,
                        principalTable: "paper_sets",
                        principalColumn: "paper_set_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_questions_question_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "question_rubrics",
                        principalColumn: "rubric_id");
                });

            migrationBuilder.CreateTable(
                name: "submission_evaluations",
                columns: table => new
                {
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_type = table.Column<int>(type: "integer", nullable: false),
                    evaluation_round = table.Column<int>(type: "integer", nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    general_feedback = table.Column<string>(type: "text", nullable: true),
                    is_final_score = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_evaluations", x => x.evaluation_id);
                    table.ForeignKey(
                        name: "FK_submission_evaluations_student_submission_submission_id",
                        column: x => x.submission_id,
                        principalTable: "student_submission",
                        principalColumn: "submission_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_parsed_answers",
                columns: table => new
                {
                    parsed_answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<Guid>(type: "uuid", nullable: true),
                    extracted_text = table.Column<string>(type: "text", nullable: true),
                    extracted_xml = table.Column<string>(type: "text", nullable: true),
                    extracted_image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_parsed_answers", x => x.parsed_answer_id);
                    table.ForeignKey(
                        name: "FK_submission_parsed_answers_diagram_elements_element_id",
                        column: x => x.element_id,
                        principalTable: "diagram_elements",
                        principalColumn: "element_id");
                    table.ForeignKey(
                        name: "FK_submission_parsed_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_submission_parsed_answers_student_submission_submission_id",
                        column: x => x.submission_id,
                        principalTable: "student_submission",
                        principalColumn: "submission_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_item_details",
                columns: table => new
                {
                    detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parsed_answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    ai_extraction_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_item_details", x => x.detail_id);
                    table.ForeignKey(
                        name: "FK_evaluation_item_details_submission_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "submission_evaluations",
                        principalColumn: "evaluation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evaluation_item_details_submission_parsed_answers_parsed_an~",
                        column: x => x.parsed_answer_id,
                        principalTable: "submission_parsed_answers",
                        principalColumn: "parsed_answer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_diagram_elements_diagram_id",
                table: "diagram_elements",
                column: "diagram_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagram_elements_element_reference_id",
                table: "diagram_elements",
                column: "element_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_item_details_evaluation_id",
                table: "evaluation_item_details",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_item_details_parsed_answer_id",
                table: "evaluation_item_details",
                column: "parsed_answer_id");

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
                column: "paper_set_id");

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
                name: "IX_question_rubrics_diagram_id",
                table: "question_rubrics",
                column: "diagram_id");

            migrationBuilder.CreateIndex(
                name: "IX_question_rubrics_paper_set_id",
                table: "question_rubrics",
                column: "paper_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_diagram_id",
                table: "questions",
                column: "diagram_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_paper_set_id",
                table: "questions",
                column: "paper_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_rubric_id",
                table: "questions",
                column: "rubric_id");

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
                name: "IX_student_semester_semester_id",
                table: "student_semester",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_diary_id",
                table: "student_submission",
                column: "diary_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_student_examination_id",
                table: "student_submission",
                column: "student_examination_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_student_id",
                table: "student_submission",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_evaluations_submission_id_evaluation_type",
                table: "submission_evaluations",
                columns: new[] { "submission_id", "evaluation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_element_id",
                table: "submission_parsed_answers",
                column: "element_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_question_id",
                table: "submission_parsed_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_parsed_answers_submission_id",
                table: "submission_parsed_answers",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_item_details");

            migrationBuilder.DropTable(
                name: "student_semester");

            migrationBuilder.DropTable(
                name: "submission_evaluations");

            migrationBuilder.DropTable(
                name: "submission_parsed_answers");

            migrationBuilder.DropTable(
                name: "diagram_elements");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "student_submission");

            migrationBuilder.DropTable(
                name: "question_rubrics");

            migrationBuilder.DropTable(
                name: "grading_diary");

            migrationBuilder.DropTable(
                name: "student_examination");

            migrationBuilder.DropTable(
                name: "diagrams");

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
