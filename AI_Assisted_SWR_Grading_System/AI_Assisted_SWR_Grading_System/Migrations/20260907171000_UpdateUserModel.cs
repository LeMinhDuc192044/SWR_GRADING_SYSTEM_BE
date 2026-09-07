using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Assisted_SWR_Grading_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Semesters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Semesters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Birthday = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Cccd = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Discriminator = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Major = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Examinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    BeforeTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SemesterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Examinations_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Folder = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LecturerId1 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_Submissions_Users_LecturerId1",
                        column: x => x.LecturerId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExamMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileDocs = table.Column<string>(type: "text", nullable: true),
                    FileRubric = table.Column<string>(type: "text", nullable: true),
                    FileAnswerTemplate = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreateById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateById1 = table.Column<string>(type: "text", nullable: true),
                    ExaminationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamMaterials_Examinations_ExaminationId",
                        column: x => x.ExaminationId,
                        principalTable: "Examinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamMaterials_Users_CreateById1",
                        column: x => x.CreateById1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Gradings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AiScore = table.Column<decimal>(type: "numeric", nullable: true),
                    LecturerScore = table.Column<decimal>(type: "numeric", nullable: true),
                    AiLogs = table.Column<string>(type: "text", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gradings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gradings_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Examinations_SemesterId",
                table: "Examinations",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamMaterials_CreateById1",
                table: "ExamMaterials",
                column: "CreateById1");

            migrationBuilder.CreateIndex(
                name: "IX_ExamMaterials_ExaminationId",
                table: "ExamMaterials",
                column: "ExaminationId");

            migrationBuilder.CreateIndex(
                name: "IX_Gradings_SubmissionId",
                table: "Gradings",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_LecturerId1",
                table: "Submissions",
                column: "LecturerId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamMaterials");

            migrationBuilder.DropTable(
                name: "Gradings");

            migrationBuilder.DropTable(
                name: "Examinations");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Semesters");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
