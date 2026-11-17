using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiModelApiKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Earlier seeding gave several default models the same key. Keep one row per key
            // (the enabled row first, oldest otherwise) and clear the duplicates, so the
            // unique index below can be created on existing databases too.
            migrationBuilder.Sql("""
                UPDATE ai_models AS model
                SET api_key = ''
                WHERE model.api_key <> ''
                  AND EXISTS (
                      SELECT 1
                      FROM ai_models AS keeper
                      WHERE keeper.api_key = model.api_key
                        AND keeper.model_id <> model.model_id
                        AND (
                            (keeper.status AND NOT model.status)
                            OR (keeper.status = model.status AND keeper.model_id < model.model_id)
                        )
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_ai_models_api_key",
                table: "ai_models",
                column: "api_key",
                unique: true,
                filter: "api_key <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_models_api_key",
                table: "ai_models");
        }
    }
}
