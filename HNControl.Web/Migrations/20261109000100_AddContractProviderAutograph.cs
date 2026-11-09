using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace HNControl.Web.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261109000100_AddContractProviderAutograph")]
public class AddContractProviderAutograph : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "ProviderSignatureStoragePath", table: "ClientLegalDocuments", type: "character varying(500)", maxLength: 500, nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("ProviderSignatureStoragePath", "ClientLegalDocuments");
}
