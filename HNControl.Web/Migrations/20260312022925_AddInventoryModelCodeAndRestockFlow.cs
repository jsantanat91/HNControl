using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryModelCodeAndRestockFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "InventoryItems"
                ADD COLUMN IF NOT EXISTS "ModelCode" character varying(40) NULL;
            """);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") AS rn
                    FROM "InventoryItems"
                    WHERE COALESCE("ModelCode", '') = ''
                )
                UPDATE "InventoryItems" i
                SET "ModelCode" = CONCAT('MDL-', LPAD(numbered.rn::text, 6, '0'))
                FROM numbered
                WHERE i."Id" = numbered."Id";
            """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_InventoryItems_ModelCode"
                ON "InventoryItems" ("ModelCode")
                WHERE "ModelCode" IS NOT NULL AND "ModelCode" <> '';
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_InventoryItems_ModelCode";
            """);

            migrationBuilder.Sql("""
                ALTER TABLE "InventoryItems" DROP COLUMN IF EXISTS "ModelCode";
            """);
        }
    }
}
