using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HNControl.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023000100_AddEngineeringNotificationEmails")]
public class AddEngineeringNotificationEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("EngineeringNotificationEmails", "SystemConfigurations",
            type: "character varying(4000)", maxLength: 4000, nullable: false,
            defaultValue: "jsantana@hubnet-solutions.net\ndtaboada@hubnet-solutions.net");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("EngineeringNotificationEmails", "SystemConfigurations");
    }
}
