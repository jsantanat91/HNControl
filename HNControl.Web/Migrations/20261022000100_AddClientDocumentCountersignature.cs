using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HNControl.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261022000100_AddClientDocumentCountersignature")]
public class AddClientDocumentCountersignature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("ProviderSignedByName", "ClientLegalDocuments", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("ProviderSignedByEmail", "ClientLegalDocuments", type: "character varying(256)", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>("ProviderSignedByUserId", "ClientLegalDocuments", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ProviderSignedAt", "ClientLegalDocuments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CompletionEmailSentAt", "ClientLegalDocuments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("SigningSnapshotJson", "ClientLegalDocuments", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ProviderSignedByName", "ClientLegalDocuments");
        migrationBuilder.DropColumn("ProviderSignedByEmail", "ClientLegalDocuments");
        migrationBuilder.DropColumn("ProviderSignedByUserId", "ClientLegalDocuments");
        migrationBuilder.DropColumn("ProviderSignedAt", "ClientLegalDocuments");
        migrationBuilder.DropColumn("CompletionEmailSentAt", "ClientLegalDocuments");
        migrationBuilder.DropColumn("SigningSnapshotJson", "ClientLegalDocuments");
    }
}
