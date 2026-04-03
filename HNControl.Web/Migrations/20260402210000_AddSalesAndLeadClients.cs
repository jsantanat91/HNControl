using System;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260402210000_AddSalesAndLeadClients")]
    public partial class AddSalesAndLeadClients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemporaryLead",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedToFormalAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesSellerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultCommissionPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesSellerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesSellerProfiles_EmployeeProfiles_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BonusDeductionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_SalesSellerProfiles_SellerProfileId",
                        column: x => x.SellerProfileId,
                        principalTable: "SalesSellerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsTemporaryLead_IsActive_CreatedAt",
                table: "Clients",
                columns: new[] { "IsTemporaryLead", "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_ClientId",
                table: "SalesOpportunities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_QuoteRequestId",
                table: "SalesOpportunities",
                column: "QuoteRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_SellerProfileId",
                table: "SalesOpportunities",
                column: "SellerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_Status_CreatedAt",
                table: "SalesOpportunities",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesSellerProfiles_EmployeeUserId",
                table: "SalesSellerProfiles",
                column: "EmployeeUserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesOpportunities");

            migrationBuilder.DropTable(
                name: "SalesSellerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsTemporaryLead_IsActive_CreatedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ConvertedToFormalAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsTemporaryLead",
                table: "Clients");
        }
    }
}
