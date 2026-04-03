using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddBillingModuleAndClientSatFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CfdiUseCodeDefault",
                table: "Clients",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegimeCode",
                table: "Clients",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalZipCode",
                table: "Clients",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingInvoicePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Concept = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    InvoiceType = table.Column<int>(type: "integer", nullable: false),
                    CfdiUseCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    FiscalRegimeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentFormCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    NextRunDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    RemainingRuns = table.Column<int>(type: "integer", nullable: true),
                    SendToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CcEmails = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoicePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_SalesOpportunities_SalesOpportunityId",
                        column: x => x.SalesOpportunityId,
                        principalTable: "SalesOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BillingInvoiceRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoiceRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInvoiceRuns_BillingInvoicePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "BillingInvoicePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_ClientId",
                table: "BillingInvoicePlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_QuoteRequestId",
                table: "BillingInvoicePlans",
                column: "QuoteRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_SalesOpportunityId",
                table: "BillingInvoicePlans",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_Status_NextRunDate",
                table: "BillingInvoicePlans",
                columns: new[] { "Status", "NextRunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRuns_PlanId_ScheduledFor",
                table: "BillingInvoiceRuns",
                columns: new[] { "PlanId", "ScheduledFor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRuns_Status_ScheduledFor",
                table: "BillingInvoiceRuns",
                columns: new[] { "Status", "ScheduledFor" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingInvoiceRuns");

            migrationBuilder.DropTable(
                name: "BillingInvoicePlans");

            migrationBuilder.DropColumn(
                name: "CfdiUseCodeDefault",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalRegimeCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalZipCode",
                table: "Clients");
        }
    }
}
