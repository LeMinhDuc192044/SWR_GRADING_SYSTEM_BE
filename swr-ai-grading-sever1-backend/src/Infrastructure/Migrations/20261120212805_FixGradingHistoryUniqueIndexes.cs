using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixGradingHistoryUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submission_grading_histories_submission_id_evaluation_type",
                table: "submission_grading_histories");

            // Reconcile duplicate official rows: keep the latest official row, demote older ones to DRAFT (Status = 0)
            migrationBuilder.Sql(@"
                WITH ranked_officials AS (
                    SELECT history_id,
                           ROW_NUMBER() OVER (PARTITION BY submission_id ORDER BY created_at DESC) as rn
                    FROM submission_grading_histories
                    WHERE ""Status"" = 1
                )
                UPDATE submission_grading_histories
                SET ""Status"" = 0
                WHERE history_id IN (
                    SELECT history_id FROM ranked_officials WHERE rn > 1
                );
            ");

            // Reconcile duplicate grading_round rows for the same submission if any exist
            migrationBuilder.Sql(@"
                WITH ranked_rounds AS (
                    SELECT history_id,
                           ROW_NUMBER() OVER (PARTITION BY submission_id, grading_round ORDER BY created_at ASC) as rn
                    FROM submission_grading_histories
                )
                UPDATE submission_grading_histories h
                SET grading_round = grading_round + r.rn - 1
                FROM ranked_rounds r
                WHERE h.history_id = r.history_id AND r.rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "ux_submission_grading_histories_one_official",
                table: "submission_grading_histories",
                column: "submission_id",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "ux_submission_grading_histories_submission_round",
                table: "submission_grading_histories",
                columns: new[] { "submission_id", "grading_round" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_submission_grading_histories_one_official",
                table: "submission_grading_histories");

            migrationBuilder.DropIndex(
                name: "ux_submission_grading_histories_submission_round",
                table: "submission_grading_histories");

            // Reconcile histories from repeated grading attempts before restoring the old constraint.
            // Retention policy: per (submission_id, evaluation_type) keep the OFFICIAL row, otherwise the
            // newest row. Every other row is copied, together with its per-question details
            // (submission_item_details.evaluation_id -> history_id), into
            // *_archive tables before it is removed, so no grading data is lost by rolling back.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE sgh_rollback_removed ON COMMIT DROP AS
                SELECT history_id FROM (
                    SELECT history_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY submission_id, evaluation_type
                               ORDER BY (""Status"" = 1) DESC, created_at DESC
                           ) AS rn
                    FROM submission_grading_histories
                ) ranked
                WHERE rn > 1;

                CREATE TABLE IF NOT EXISTS submission_grading_histories_archive AS
                SELECT * FROM submission_grading_histories WHERE 1=0;

                CREATE TABLE IF NOT EXISTS submission_item_details_archive AS
                SELECT * FROM submission_item_details WHERE 1=0;

                INSERT INTO submission_grading_histories_archive
                SELECT h.* FROM submission_grading_histories h
                WHERE h.history_id IN (SELECT history_id FROM sgh_rollback_removed);

                INSERT INTO submission_item_details_archive
                SELECT d.* FROM submission_item_details d
                WHERE d.evaluation_id IN (SELECT history_id FROM sgh_rollback_removed);

                DELETE FROM submission_item_details
                WHERE evaluation_id IN (SELECT history_id FROM sgh_rollback_removed);

                DELETE FROM submission_grading_histories
                WHERE history_id IN (SELECT history_id FROM sgh_rollback_removed);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_submission_grading_histories_submission_id_evaluation_type",
                table: "submission_grading_histories",
                columns: new[] { "submission_id", "evaluation_type" },
                unique: true);
        }
    }
}
