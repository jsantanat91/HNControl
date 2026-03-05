using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBasePro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessNotes",
                table: "KnowledgeLinks",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccessSecretProtected",
                table: "KnowledgeLinks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccessUsername",
                table: "KnowledgeLinks",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "KnowledgeLinks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentOriginalFileName",
                table: "KnowledgeLinks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSizeBytes",
                table: "KnowledgeLinks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentStoragePath",
                table: "KnowledgeLinks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "KnowledgeLinks",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DocType",
                table: "KnowledgeLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "KnowledgeLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastViewedAt",
                table: "KnowledgeLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "KnowledgeLinks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "KnowledgeLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "KnowledgeLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDueAt",
                table: "KnowledgeLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "KnowledgeLinks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "KnowledgeLinks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "KnowledgeLinks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "KnowledgeLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByName",
                table: "KnowledgeLinks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "KnowledgeLinks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "KnowledgeLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_IsPinned",
                table: "KnowledgeLinks",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ReviewDueAt",
                table: "KnowledgeLinks",
                column: "ReviewDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_Status_DocType_Category",
                table: "KnowledgeLinks",
                columns: new[] { "Status", "DocType", "Category" });

            migrationBuilder.Sql("""
                UPDATE "KnowledgeLinks"
                SET "PublishedAt" = COALESCE("PublishedAt", "CreatedAt"),
                    "UpdatedAt" = COALESCE(NULLIF("UpdatedAt", '0001-01-01 00:00:00+00'::timestamp with time zone), "CreatedAt", NOW()),
                    "Status" = COALESCE("Status", 1),
                    "Version" = CASE WHEN "Version" IS NULL OR "Version" < 1 THEN 1 ELSE "Version" END
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_IsPinned",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_ReviewDueAt",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_Status_DocType_Category",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AccessNotes",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AccessSecretProtected",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AccessUsername",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AttachmentOriginalFileName",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AttachmentSizeBytes",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "AttachmentStoragePath",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "DocType",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "LastViewedAt",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ReviewDueAt",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "UpdatedByName",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "KnowledgeLinks");
        }
    }
}
