using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace HNControl.Web.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261109000200_AddOrderDelivery")]
public class AddOrderDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "DeliveryJson", table: "ServiceOrders", type: "text", nullable: false, defaultValue: "");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("DeliveryJson", "ServiceOrders");
}
