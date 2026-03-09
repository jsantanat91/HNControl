using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRulesPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuoteCatalogRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Segment = table.Column<int>(type: "integer", nullable: false),
                    TargetItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteCatalogRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteCatalogRules_Segment_IsActive",
                table: "QuoteCatalogRules",
                columns: new[] { "Segment", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteCatalogRules_Segment_TargetItemId_RequiredItemId",
                table: "QuoteCatalogRules",
                columns: new[] { "Segment", "TargetItemId", "RequiredItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteCatalogRules");
        }
    }
}
