using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingDiaryAndSubmissionDiaryFK : Migration
    {
        /// <inheritdoc />
        ///
        /// LƯU Ý QUAN TRỌNG:
        ///   Bảng <c>grading_diary</c> đã được tạo bởi teammate trên DB trước đó.
        ///   EF generate ra <c>CreateTable</c> → sẽ fail với "relation already exists".
        ///   → Ta wrap trong <c>CREATE TABLE IF NOT EXISTS</c> để migration idempotent.
        ///   Sau đó dùng <c>ALTER TABLE IF EXISTS</c> cho FK để chắc chắn state mong muốn.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Drop FK cũ từ submissions → lecturers (nếu còn)
            migrationBuilder.Sql(@"
                ALTER TABLE submissions
                DROP CONSTRAINT IF EXISTS ""FK_submissions_lecturers_lecturer_id"";
            ");

            // 2) Rename cột lecturer_id → diary_id (idempotent: dùng DO block kiểm tra)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'submissions' AND column_name = 'lecturer_id'
                    ) AND NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'submissions' AND column_name = 'diary_id'
                    ) THEN
                        ALTER TABLE submissions RENAME COLUMN lecturer_id TO diary_id;
                    END IF;
                END $$;
            ");

            // 3) Đảm bảo index đúng tên
            migrationBuilder.Sql(@"
                ALTER INDEX IF EXISTS ""IX_submissions_lecturer_id""
                RENAME TO ""IX_submissions_diary_id"";
            ");

            // 4) Tạo bảng grading_diary nếu chưa có (run an toàn dù teammate đã tạo sẵn)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS grading_diary (
                    grading_diary_id uuid PRIMARY KEY,
                    examination_code character varying(50) NOT NULL,
                    name character varying(200) NOT NULL,
                    create_by_id uuid NOT NULL
                );
            ");

            // 5) Index cho create_by_id
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_grading_diary_create_by_id""
                ON grading_diary (create_by_id);
            ");

            // 6) FK từ grading_diary.create_by_id → lecturers.id (nếu chưa có)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_grading_diary_lecturers_create_by_id'
                    ) THEN
                        ALTER TABLE grading_diary
                        ADD CONSTRAINT ""FK_grading_diary_lecturers_create_by_id""
                        FOREIGN KEY (create_by_id) REFERENCES lecturers(id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            // 7) FK từ submissions.diary_id → grading_diary.grading_diary_id (nếu chưa có)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_submissions_grading_diary_diary_id'
                    ) THEN
                        ALTER TABLE submissions
                        ADD CONSTRAINT ""FK_submissions_grading_diary_diary_id""
                        FOREIGN KEY (diary_id) REFERENCES grading_diary(grading_diary_id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK vừa thêm (nếu còn)
            migrationBuilder.Sql(@"
                ALTER TABLE submissions
                DROP CONSTRAINT IF EXISTS ""FK_submissions_grading_diary_diary_id"";
            ");

            // KHÔNG drop bảng grading_diary trong Down() — vì teammate có thể vẫn cần
            // (rollback chỉ undo phần liên quan tới submissions)
            migrationBuilder.Sql(@"
                ALTER TABLE submissions
                DROP CONSTRAINT IF EXISTS ""FK_submissions_lecturers_lecturer_id"";
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'submissions' AND column_name = 'diary_id'
                    ) AND NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'submissions' AND column_name = 'lecturer_id'
                    ) THEN
                        ALTER TABLE submissions RENAME COLUMN diary_id TO lecturer_id;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                ALTER INDEX IF EXISTS ""IX_submissions_diary_id""
                RENAME TO ""IX_submissions_lecturer_id"";
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_submissions_lecturers_lecturer_id'
                    ) THEN
                        ALTER TABLE submissions
                        ADD CONSTRAINT ""FK_submissions_lecturers_lecturer_id""
                        FOREIGN KEY (lecturer_id) REFERENCES lecturers(id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
        }
    }
}
