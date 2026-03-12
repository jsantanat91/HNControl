using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollReceiptDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ModelCode",
                table: "InventoryItems");

            migrationBuilder.CreateTable(
                name: "PayrollReceiptDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollReceiptDispatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ModelCode",
                table: "InventoryItems",
                column: "ModelCode",
                unique: true,
                filter: "\"ModelCode\" IS NOT NULL AND \"ModelCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollReceiptDispatches_PayrollDate_IsSent",
                table: "PayrollReceiptDispatches",
                columns: new[] { "PayrollDate", "IsSent" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollReceiptDispatches_UserId_PeriodStart_PeriodEnd",
                table: "PayrollReceiptDispatches",
                columns: new[] { "UserId", "PeriodStart", "PeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollReceiptDispatches");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_ModelCode",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ModelCode",
                table: "InventoryItems",
                column: "ModelCode",
                unique: true);
        }
    }
}
