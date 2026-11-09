using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace HNControl.Web.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261109232629_AddServiceOrderCommissions")]
public class AddServiceOrderCommissions : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>("ServiceOrderCommissionEnabled", "EmployeeProfiles", type: "boolean", nullable: false, defaultValue: false);
        m.AddColumn<decimal>("ServiceOrderCommissionAmount", "EmployeeProfiles", type: "numeric(18,2)", nullable: false, defaultValue: 0m);
        m.AddColumn<Guid>("SourceServiceOrderId", "EmployeeDeductions", type: "uuid", nullable: true);
        m.CreateIndex("IX_EmployeeDeductions_SourceServiceOrderId", "EmployeeDeductions", "SourceServiceOrderId", unique: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex("IX_EmployeeDeductions_SourceServiceOrderId", "EmployeeDeductions");
        m.DropColumn("SourceServiceOrderId", "EmployeeDeductions");
        m.DropColumn("ServiceOrderCommissionAmount", "EmployeeProfiles");
        m.DropColumn("ServiceOrderCommissionEnabled", "EmployeeProfiles");
    }
}
