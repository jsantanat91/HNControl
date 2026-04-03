using System;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260402193000_AddClientLegalDocsAndDeliveryFormats")]
    public partial class AddClientLegalDocsAndDeliveryFormats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""BillingEmail"" character varying(256);");
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""BusinessLine"" character varying(180);");
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""FiscalAddress"" character varying(400);");
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""LegalEmail"" character varying(256);");
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""LegalPosition"" character varying(120);");
            migrationBuilder.Sql(@"ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""LegalRepresentative"" character varying(160);");

            migrationBuilder.CreateTable(
                name: "ClientLegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientServiceContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TermsBody = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ContractStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SignatureStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientLegalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientLegalDocuments_ClientServiceContracts_ClientServiceCon~",
                        column: x => x.ClientServiceContractId,
                        principalTable: "ClientServiceContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientLegalDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDeliveryFormats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    ServiceSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EquipmentSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DeliveryLocation = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ReceiverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReceiverEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReceiverPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SignatureStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeliveryFormats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDeliveryFormats_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDeliveryFormats_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_ClientId_DocumentType_Status",
                table: "ClientLegalDocuments",
                columns: new[] { "ClientId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_ClientServiceContractId",
                table: "ClientLegalDocuments",
                column: "ClientServiceContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_PublicToken",
                table: "ClientLegalDocuments",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_ClientId_Status_CreatedAt",
                table: "ProjectDeliveryFormats",
                columns: new[] { "ClientId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_ProjectId",
                table: "ProjectDeliveryFormats",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_PublicToken",
                table: "ProjectDeliveryFormats",
                column: "PublicToken",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientLegalDocuments");

            migrationBuilder.DropTable(
                name: "ProjectDeliveryFormats");

            migrationBuilder.DropColumn(
                name: "BillingEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BusinessLine",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalAddress",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalPosition",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentative",
                table: "Clients");
        }
    }
}
