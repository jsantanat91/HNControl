using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddSystemConfigurationModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CompanyLegalName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CompanyRfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    CompanyFiscalRegimeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CompanyFiscalZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompanyFiscalAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    BillingEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CompanyLogoStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompanyLogoOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SmtpHost = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    SmtpUser = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SmtpPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    SmtpFromEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SmtpFromName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SmtpSecurity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SmtpHeloDomain = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SmtpTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    BillingPacProvider = table.Column<int>(type: "integer", nullable: false),
                    BillingPacApiBaseUrl = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BillingPacApiKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BillingPacApiSecretProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    BillingPacUsername = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    BillingPacPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    CfdiVersion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CfdiSerieDefault = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CsdCerStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CsdKeyStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CsdPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigurations_UpdatedAt",
                table: "SystemConfigurations",
                column: "UpdatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigurations");
        }
    }
}
