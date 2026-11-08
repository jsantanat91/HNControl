using System;
using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261108000100_AddServiceInventory")]
public class AddServiceInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ServiceInventoryAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CarrierKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                OtherCarrierName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Label = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AccountNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PasswordProtected = table.Column<string>(type: "text", nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceInventoryAccounts", x => x.Id);
            });

        migrationBuilder.CreateIndex("IX_ServiceInventoryAccounts_CarrierKey_IsActive", "ServiceInventoryAccounts", new[] { "CarrierKey", "IsActive" });

        migrationBuilder.CreateTable(
            name: "ServiceInventoryItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CarrierKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                OtherCarrierName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                AccountNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PlanName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                PlanType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DeliveryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DeviceModel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                KitNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SpeedMbps = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                MonthlyCost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                PhysicalLocation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Lan = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Ddns = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                SupportPhone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                FixedIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SubnetMask = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Gateway = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Dns = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContractMonths = table.Column<int>(type: "integer", nullable: true),
                ContractStartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                ContractEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                Notes = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceInventoryItems", x => x.Id);
                table.ForeignKey("FK_ServiceInventoryItems_ServiceInventoryAccounts_AccountId", x => x.AccountId, "ServiceInventoryAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ServiceInventoryItems_Clients_ClientId", x => x.ClientId, "Clients", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_ServiceInventoryItems_AccountId", "ServiceInventoryItems", "AccountId");
        migrationBuilder.CreateIndex("IX_ServiceInventoryItems_ClientId", "ServiceInventoryItems", "ClientId");
        migrationBuilder.CreateIndex("IX_ServiceInventoryItems_CarrierKey_IsActive", "ServiceInventoryItems", new[] { "CarrierKey", "IsActive" });

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ServiceInventoryItems");
        migrationBuilder.DropTable("ServiceInventoryAccounts");
    }
}
