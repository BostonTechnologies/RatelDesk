using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCategoryUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "_ticket_category_dedup_map" AS
                WITH ranked AS (
                    SELECT
                        "Id",
                        "Name",
                        "Type",
                        "TenantId",
                        ROW_NUMBER() OVER (
                            PARTITION BY "Name", "Type", "TenantId"
                            ORDER BY "CreatedUtc", "Id"
                        ) AS rn,
                        FIRST_VALUE("Id") OVER (
                            PARTITION BY "Name", "Type", "TenantId"
                            ORDER BY "CreatedUtc", "Id"
                        ) AS keep_id
                    FROM "TicketCategories"
                )
                SELECT "Id" AS old_id, keep_id
                FROM ranked
                WHERE rn > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE "TicketCategories" c
                SET "ParentCategoryId" = m.keep_id
                FROM "_ticket_category_dedup_map" m
                WHERE c."ParentCategoryId" = m.old_id;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "IncidentCategoryLinks" ("IncidentId", "TicketCategoryId")
                SELECT DISTINCT l."IncidentId", COALESCE(m.keep_id, l."TicketCategoryId")
                FROM "IncidentCategoryLinks" l
                LEFT JOIN "_ticket_category_dedup_map" m ON l."TicketCategoryId" = m.old_id
                ON CONFLICT DO NOTHING;

                DELETE FROM "IncidentCategoryLinks" l
                USING "_ticket_category_dedup_map" m
                WHERE l."TicketCategoryId" = m.old_id;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "RequestCategoryLinks" ("RequestId", "TicketCategoryId")
                SELECT DISTINCT l."RequestId", COALESCE(m.keep_id, l."TicketCategoryId")
                FROM "RequestCategoryLinks" l
                LEFT JOIN "_ticket_category_dedup_map" m ON l."TicketCategoryId" = m.old_id
                ON CONFLICT DO NOTHING;

                DELETE FROM "RequestCategoryLinks" l
                USING "_ticket_category_dedup_map" m
                WHERE l."TicketCategoryId" = m.old_id;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "ChangeCategoryLinks" ("ChangeId", "TicketCategoryId")
                SELECT DISTINCT l."ChangeId", COALESCE(m.keep_id, l."TicketCategoryId")
                FROM "ChangeCategoryLinks" l
                LEFT JOIN "_ticket_category_dedup_map" m ON l."TicketCategoryId" = m.old_id
                ON CONFLICT DO NOTHING;

                DELETE FROM "ChangeCategoryLinks" l
                USING "_ticket_category_dedup_map" m
                WHERE l."TicketCategoryId" = m.old_id;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "TicketCategories" c
                USING "_ticket_category_dedup_map" m
                WHERE c."Id" = m.old_id;
                """);

            migrationBuilder.Sql("""DROP TABLE "_ticket_category_dedup_map";""");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_Name_Type_TenantId",
                table: "TicketCategories",
                columns: new[] { "Name", "Type", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketCategories_Name_Type_TenantId",
                table: "TicketCategories");
        }
    }
}
