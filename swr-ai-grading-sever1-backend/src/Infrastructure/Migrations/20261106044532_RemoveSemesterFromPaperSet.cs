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
            migrationBuilder.CreateTable(
                name: "ai_models",
                columns: table => new
                {
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    api_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<bool>(type: "boolean", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    is_selected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_models", x => x.model_id);
                });

            migrationBuilder.CreateTable(
                name: "diagrams",
                columns: table => new
                {
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
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
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    examination_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
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
                name: "staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    staff_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_users_id",
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
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_id = table.Column<Guid>(type: "uuid", nullable: true)
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
                        name: "FK_paper_sets_staff_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.application_id);
                    table.ForeignKey(
                        name: "FK_applications_students_student_id",
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
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                        name: "FK_student_semester_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_semester_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "examination_sessions",
                columns: table => new
                {
                    examination_sessions_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    created_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_examination_sessions", x => x.examination_sessions_id);
                    table.ForeignKey(
                        name: "FK_examination_sessions_examinations_examination_id",
                        column: x => x.examination_id,
                        principalTable: "examinations",
                        principalColumn: "examination_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_examination_sessions_paper_sets_paper_set_id",
                        column: x => x.paper_set_id,
                        principalTable: "paper_sets",
                        principalColumn: "paper_set_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_examination_sessions_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "semester_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    point = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    paper_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagram_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "grading_sessions",
                columns: table => new
                {
                    grading_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    number_submissions = table.Column<int>(type: "integer", nullable: false),
                    examination_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperSetId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_sessions", x => x.grading_session_id);
                    table.ForeignKey(
                        name: "FK_grading_sessions_examination_sessions_examination_session_id",
                        column: x => x.examination_session_id,
                        principalTable: "examination_sessions",
                        principalColumn: "examination_sessions_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grading_sessions_paper_sets_PaperSetId",
                        column: x => x.PaperSetId,
                        principalTable: "paper_sets",
                        principalColumn: "paper_set_id");
                });

            migrationBuilder.CreateTable(
                name: "student_examination_sessions",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_examination_sessions", x => new { x.student_id, x.examination_session_id });
                    table.ForeignKey(
                        name: "FK_student_examination_sessions_examination_sessions_examinati~",
                        column: x => x.examination_session_id,
                        principalTable: "examination_sessions",
                        principalColumn: "examination_sessions_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_examination_sessions_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
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
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    examination_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_submission", x => x.submission_id);
                    table.ForeignKey(
                        name: "FK_student_submission_examination_sessions_examination_session~",
                        column: x => x.examination_session_id,
                        principalTable: "examination_sessions",
                        principalColumn: "examination_sessions_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_submission_staff_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_submission_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_criterias",
                columns: table => new
                {
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criteria = table.Column<string>(type: "text", nullable: false),
                    point = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_criterias", x => x.rubric_id);
                    table.ForeignKey(
                        name: "FK_question_criterias_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_status = table.Column<int>(type: "integer", nullable: false),
                    grading_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.assignment_id);
                    table.ForeignKey(
                        name: "FK_assignments_grading_sessions_grading_session_id",
                        column: x => x.grading_session_id,
                        principalTable: "grading_sessions",
                        principalColumn: "grading_session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_student_submission_submission_id",
                        column: x => x.submission_id,
                        principalTable: "student_submission",
                        principalColumn: "submission_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_grading_histories",
                columns: table => new
                {
                    history_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_type = table.Column<int>(type: "integer", nullable: false),
                    grading_round = table.Column<int>(type: "integer", nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    general_feedback = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_grading_histories", x => x.history_id);
                    table.ForeignKey(
                        name: "FK_submission_grading_histories_student_submission_submission_~",
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
                    element_id = table.Column<Guid>(type: "uuid", nullable: true),
                    extracted_text = table.Column<string>(type: "text", nullable: true),
                    extracted_xml = table.Column<string>(type: "text", nullable: true),
                    extracted_image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "submission_item_details",
                columns: table => new
                {
                    detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parsed_answer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_item_details", x => x.detail_id);
                    table.ForeignKey(
                        name: "FK_submission_item_details_submission_grading_histories_evalua~",
                        column: x => x.evaluation_id,
                        principalTable: "submission_grading_histories",
                        principalColumn: "history_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_submission_item_details_submission_parsed_answers_parsed_an~",
                        column: x => x.parsed_answer_id,
                        principalTable: "submission_parsed_answers",
                        principalColumn: "parsed_answer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_student_id",
                table: "applications",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_grading_session_id",
                table: "assignments",
                column: "grading_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_staff_id",
                table: "assignments",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_submission_id",
                table: "assignments",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagram_elements_diagram_id",
                table: "diagram_elements",
                column: "diagram_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagram_elements_element_reference_id",
                table: "diagram_elements",
                column: "element_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_examination_sessions_examination_id",
                table: "examination_sessions",
                column: "examination_id");

            migrationBuilder.CreateIndex(
                name: "IX_examination_sessions_paper_set_id",
                table: "examination_sessions",
                column: "paper_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_examination_sessions_semester_id",
                table: "examination_sessions",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_examinations_semester_id",
                table: "examinations",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_grading_sessions_examination_session_id",
                table: "grading_sessions",
                column: "examination_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_grading_sessions_PaperSetId",
                table: "grading_sessions",
                column: "PaperSetId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_examination_id",
                table: "paper_sets",
                column: "examination_id");

            migrationBuilder.CreateIndex(
                name: "IX_paper_sets_uploaded_by_id",
                table: "paper_sets",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_question_criterias_question_id",
                table: "question_criterias",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_diagram_id",
                table: "questions",
                column: "diagram_id");

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
                name: "IX_staff_staff_code",
                table: "staff",
                column: "staff_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_examination_sessions_examination_session_id",
                table: "student_examination_sessions",
                column: "examination_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_semester_semester_id",
                table: "student_semester",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_semester_staff_id",
                table: "student_semester",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_examination_session_id",
                table: "student_submission",
                column: "examination_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_student_id",
                table: "student_submission",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_submission_uploaded_by_id",
                table: "student_submission",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_grading_histories_submission_id_evaluation_type",
                table: "submission_grading_histories",
                columns: new[] { "submission_id", "evaluation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_item_details_evaluation_id",
                table: "submission_item_details",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_item_details_parsed_answer_id",
                table: "submission_item_details",
                column: "parsed_answer_id");

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
                name: "ai_models");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropTable(
                name: "question_criterias");

            migrationBuilder.DropTable(
                name: "student_examination_sessions");

            migrationBuilder.DropTable(
                name: "student_semester");

            migrationBuilder.DropTable(
                name: "submission_item_details");

            migrationBuilder.DropTable(
                name: "grading_sessions");

            migrationBuilder.DropTable(
                name: "submission_grading_histories");

            migrationBuilder.DropTable(
                name: "submission_parsed_answers");

            migrationBuilder.DropTable(
                name: "diagram_elements");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "student_submission");

            migrationBuilder.DropTable(
                name: "diagrams");

            migrationBuilder.DropTable(
                name: "examination_sessions");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "paper_sets");

            migrationBuilder.DropTable(
                name: "examinations");

            migrationBuilder.DropTable(
                name: "staff");

            migrationBuilder.DropTable(
                name: "semesters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
