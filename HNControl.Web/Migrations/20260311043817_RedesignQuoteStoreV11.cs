using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class RedesignQuoteStoreV11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "QuoteRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedByUserId",
                table: "QuoteRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemImageUrl",
                table: "QuoteRequestLines",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferType",
                table: "QuoteRequestLines",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "QuoteCatalogItems",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferType",
                table: "QuoteCatalogItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "VariantGroup",
                table: "QuoteCatalogItems",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantValue",
                table: "QuoteCatalogItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedByUserId",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "ItemImageUrl",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "OfferType",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "QuoteCatalogItems");

            migrationBuilder.DropColumn(
                name: "OfferType",
                table: "QuoteCatalogItems");

            migrationBuilder.DropColumn(
                name: "VariantGroup",
                table: "QuoteCatalogItems");

            migrationBuilder.DropColumn(
                name: "VariantValue",
                table: "QuoteCatalogItems");
        }
    }
}
