using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierServiceContractAndMonitorCarrierLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientCarrierServiceId",
                table: "MonitorTargets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientServiceContractId",
                table: "ClientCarrierServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitorTargets_ClientCarrierServiceId",
                table: "MonitorTargets",
                column: "ClientCarrierServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCarrierServices_ClientServiceContractId",
                table: "ClientCarrierServices",
                column: "ClientServiceContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientCarrierServices_ClientServiceContracts_ClientServiceC~",
                table: "ClientCarrierServices",
                column: "ClientServiceContractId",
                principalTable: "ClientServiceContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MonitorTargets_ClientCarrierServices_ClientCarrierServiceId",
                table: "MonitorTargets",
                column: "ClientCarrierServiceId",
                principalTable: "ClientCarrierServices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: intentar ligar servicios carrier con contratos por cliente + cuenta/contrato.
            migrationBuilder.Sql("""
                UPDATE "ClientCarrierServices" s
                SET "ClientServiceContractId" = c."Id"
                FROM "ClientServiceContracts" c
                WHERE s."ClientServiceContractId" IS NULL
                  AND s."ClientId" = c."ClientId"
                  AND (
                        (NULLIF(s."ContractNumber",'') IS NOT NULL AND s."ContractNumber" = c."ContractNumber")
                     OR (NULLIF(s."AccountNumber",'')  IS NOT NULL AND s."AccountNumber"  = c."AccountNumber")
                  );
            """);

            // Backfill: si target ya tenia contrato, ligar automaticamente al primer servicio carrier de ese contrato.
            migrationBuilder.Sql("""
                UPDATE "MonitorTargets" t
                SET "ClientCarrierServiceId" = s."Id"
                FROM "ClientCarrierServices" s
                WHERE t."ClientCarrierServiceId" IS NULL
                  AND t."ClientServiceContractId" IS NOT NULL
                  AND s."ClientServiceContractId" = t."ClientServiceContractId"
                  AND s."IsActive" = TRUE;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientCarrierServices_ClientServiceContracts_ClientServiceC~",
                table: "ClientCarrierServices");

            migrationBuilder.DropForeignKey(
                name: "FK_MonitorTargets_ClientCarrierServices_ClientCarrierServiceId",
                table: "MonitorTargets");

            migrationBuilder.DropIndex(
                name: "IX_MonitorTargets_ClientCarrierServiceId",
                table: "MonitorTargets");

            migrationBuilder.DropIndex(
                name: "IX_ClientCarrierServices_ClientServiceContractId",
                table: "ClientCarrierServices");

            migrationBuilder.DropColumn(
                name: "ClientCarrierServiceId",
                table: "MonitorTargets");

            migrationBuilder.DropColumn(
                name: "ClientServiceContractId",
                table: "ClientCarrierServices");
        }
    }
}
