using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class QuoteVat16Support : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalBeforeVat",
                table: "QuoteRequests",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "QuoteRequests",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "QuoteRequestLines",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PriceIncludesVat",
                table: "QuoteRequestLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "QuoteRequestLines",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "QuoteRequestLines",
                type: "numeric(6,4)",
                nullable: false,
                defaultValue: 0.16m);

            migrationBuilder.AddColumn<bool>(
                name: "UnitPriceIncludesVat",
                table: "QuoteCatalogItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubtotalBeforeVat",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "PriceIncludesVat",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "UnitPriceIncludesVat",
                table: "QuoteCatalogItems");
        }
    }
}
