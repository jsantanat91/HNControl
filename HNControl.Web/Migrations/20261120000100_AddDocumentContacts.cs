using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HNControl.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261120000100_AddDocumentContacts")]
public class AddDocumentContacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "ClientServiceContracts", "ClientLegalDocuments", "ServiceOrders" })
            migrationBuilder.AddColumn<string>("NotificationContactsJson", table, type: "text", nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "ClientServiceContracts", "ClientLegalDocuments", "ServiceOrders" })
            migrationBuilder.DropColumn("NotificationContactsJson", table);
    }
}
