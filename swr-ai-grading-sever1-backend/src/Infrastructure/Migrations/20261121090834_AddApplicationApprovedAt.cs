using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationApprovedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            // Appeals approved before this column existed have no recorded approval time.
            // updated_day is exact for APPROVED (1) but is the later IN_PROGRESS time for IN_PROGRESS (3),
            // which could push regrade drafts already written out of the cycle. So the cutoff is the
            // earlier of updated_day and just before the first history newer than the latest official
            // round: every draft written after the last official grade stays in the regrade cycle.
            migrationBuilder.Sql(@"
                UPDATE applications a
                SET approved_at = LEAST(
                    a.updated_day,
                    COALESCE((
                        SELECT MIN(h.created_at) - INTERVAL '1 millisecond'
                        FROM submission_grading_histories h
                        WHERE h.submission_id = a.submission_id
                          AND h.grading_round > COALESCE((
                              SELECT MAX(o.grading_round)
                              FROM submission_grading_histories o
                              WHERE o.submission_id = a.submission_id AND o.""Status"" = 1), 0)
                    ), a.updated_day))
                WHERE a.""Status"" IN (1, 3) AND a.approved_at IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "applications");
        }
    }
}
