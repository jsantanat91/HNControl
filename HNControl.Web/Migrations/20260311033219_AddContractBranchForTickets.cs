using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddContractBranchForTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ClientServiceContracts"
                ADD COLUMN IF NOT EXISTS "Branch" character varying(140) NOT NULL DEFAULT '';
            """);

            migrationBuilder.Sql("""
                ALTER TABLE "ClientServiceContracts"
                ADD COLUMN IF NOT EXISTS "BranchAddress" character varying(320) NOT NULL DEFAULT '';
            """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "TicketAttachments" (
                    "Id" uuid NOT NULL,
                    "TicketId" uuid NOT NULL,
                    "OriginalFileName" character varying(255) NOT NULL,
                    "ContentType" character varying(100) NOT NULL,
                    "StoragePath" character varying(500) NOT NULL,
                    "SizeBytes" bigint NOT NULL,
                    "UploadedAt" timestamp with time zone NOT NULL,
                    "UploadedByUserId" character varying(64) NOT NULL,
                    "UploadedByName" character varying(200) NOT NULL,
                    CONSTRAINT "PK_TicketAttachments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_TicketAttachments_Tickets_TicketId"
                        FOREIGN KEY ("TicketId")
                        REFERENCES "Tickets" ("Id")
                        ON DELETE CASCADE
                );
            """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_TicketAttachments_TicketId_UploadedAt"
                ON "TicketAttachments" ("TicketId", "UploadedAt");
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_TicketAttachments_TicketId_UploadedAt";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "TicketAttachments";""");
            migrationBuilder.Sql("""ALTER TABLE "ClientServiceContracts" DROP COLUMN IF EXISTS "Branch";""");
            migrationBuilder.Sql("""ALTER TABLE "ClientServiceContracts" DROP COLUMN IF EXISTS "BranchAddress";""");
        }
    }
}
