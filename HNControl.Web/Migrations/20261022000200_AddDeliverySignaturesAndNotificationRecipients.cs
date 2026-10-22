using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HNControl.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261022000200_AddDeliverySignaturesAndNotificationRecipients")]
public class AddDeliverySignaturesAndNotificationRecipients : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("DocumentNotificationEmails", "SystemConfigurations", type: "character varying(4000)", maxLength: 4000, nullable: false,
            defaultValue: "jsantana@hubnet-solutions.net\ndtaboada@hubnet-solutions.net");
        migrationBuilder.AddColumn<string>("ProviderSignedByName", "ProjectDeliveryFormats", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("ProviderSignedByEmail", "ProjectDeliveryFormats", type: "character varying(256)", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>("ProviderSignedByUserId", "ProjectDeliveryFormats", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ProviderSignedAt", "ProjectDeliveryFormats", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CompletionEmailSentAt", "ProjectDeliveryFormats", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("SigningSnapshotJson", "ProjectDeliveryFormats", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("DocumentNotificationEmails", "SystemConfigurations");
        migrationBuilder.DropColumn("ProviderSignedByName", "ProjectDeliveryFormats");
        migrationBuilder.DropColumn("ProviderSignedByEmail", "ProjectDeliveryFormats");
        migrationBuilder.DropColumn("ProviderSignedByUserId", "ProjectDeliveryFormats");
        migrationBuilder.DropColumn("ProviderSignedAt", "ProjectDeliveryFormats");
        migrationBuilder.DropColumn("CompletionEmailSentAt", "ProjectDeliveryFormats");
        migrationBuilder.DropColumn("SigningSnapshotJson", "ProjectDeliveryFormats");
    }
}
