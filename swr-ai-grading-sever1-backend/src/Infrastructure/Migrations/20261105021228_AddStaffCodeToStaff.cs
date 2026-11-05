using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffCodeToStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "staff_code",
                table: "staff",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE staff
                SET staff_code = 'STAFF-' || substring(id::text, 1, 14)
                WHERE staff_code = '' OR staff_code IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "staff_code",
                table: "staff",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: false,
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_staff_staff_code",
                table: "staff",
                column: "staff_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_staff_code",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "staff_code",
                table: "staff");
        }
    }
}
