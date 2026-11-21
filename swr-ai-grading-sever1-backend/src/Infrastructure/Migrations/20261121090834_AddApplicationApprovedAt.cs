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

            // Appeals approved before this column existed: their last update is the best known
            // approval time (APPROVED = 1, IN_PROGRESS = 3).
            migrationBuilder.Sql(@"
                UPDATE applications
                SET approved_at = updated_day
                WHERE ""Status"" IN (1, 3) AND approved_at IS NULL;
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
