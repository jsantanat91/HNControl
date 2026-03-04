using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddServiceOrderClaimAndWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_AssignedUserId",
                table: "ServiceOrders");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedUserId",
                table: "ServiceOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "ServiceOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedByUserId",
                table: "ServiceOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentArea",
                table: "ServiceOrders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_ClaimedByUserId",
                table: "ServiceOrders",
                column: "ClaimedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_AssignedUserId",
                table: "ServiceOrders",
                column: "AssignedUserId",
                principalTable: "EmployeeProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_ClaimedByUserId",
                table: "ServiceOrders",
                column: "ClaimedByUserId",
                principalTable: "EmployeeProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_AssignedUserId",
                table: "ServiceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_ClaimedByUserId",
                table: "ServiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_ClaimedByUserId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "CurrentArea",
                table: "ServiceOrders");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedUserId",
                table: "ServiceOrders",
                type: "character varying(64)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_EmployeeProfiles_AssignedUserId",
                table: "ServiceOrders",
                column: "AssignedUserId",
                principalTable: "EmployeeProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
