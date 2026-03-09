using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteModulePhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicQuoteToken",
                table: "Clients",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuoteCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Segment = table.Column<int>(type: "integer", nullable: false),
                    NodeType = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    IsManualPrice = table.Column<bool>(type: "boolean", nullable: false),
                    ReferenceUrl = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuoteRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Folio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Segment = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerLocation = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    SubtotalAuto = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ManualItemsCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuoteRequestLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    SubproductName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    Description = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    IsManualPrice = table.Column<bool>(type: "boolean", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteRequestLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteRequestLines_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PublicQuoteToken",
                table: "Clients",
                column: "PublicQuoteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteCatalogItems_Segment_NodeType_ParentId_IsActive",
                table: "QuoteCatalogItems",
                columns: new[] { "Segment", "NodeType", "ParentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequestLines_QuoteRequestId",
                table: "QuoteRequestLines",
                column: "QuoteRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequests_Folio",
                table: "QuoteRequests",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuoteRequests_Segment_CreatedAt",
                table: "QuoteRequests",
                columns: new[] { "Segment", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteCatalogItems");

            migrationBuilder.DropTable(
                name: "QuoteRequestLines");

            migrationBuilder.DropTable(
                name: "QuoteRequests");

            migrationBuilder.DropIndex(
                name: "IX_Clients_PublicQuoteToken",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PublicQuoteToken",
                table: "Clients");
        }
    }
}
