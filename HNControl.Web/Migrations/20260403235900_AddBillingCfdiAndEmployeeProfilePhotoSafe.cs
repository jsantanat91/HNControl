using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddBillingCfdiAndEmployeeProfilePhotoSafe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""CfdiUuid"" character varying(80);");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""CfdiStatus"" integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""CancelReasonCode"" character varying(4);");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""SatStatusMessage"" character varying(1000);");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""PacTrackingId"" character varying(120);");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""LastSyncAt"" timestamp with time zone;");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""CancellationRequestedAt"" timestamp with time zone;");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" ADD COLUMN IF NOT EXISTS ""CancelledAt"" timestamp with time zone;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_BillingInvoiceRuns_CfdiStatus_LastSyncAt"" ON ""BillingInvoiceRuns"" (""CfdiStatus"", ""LastSyncAt"");");

            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""EducationLevel"" character varying(120);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""Rfc"" character varying(13);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""PostalCode"" character varying(10);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""EmployeeNumber"" character varying(40);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""SatContractTypeCode"" character varying(4);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""SatWorkdayTypeCode"" character varying(4);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""SatJobRiskCode"" character varying(4);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""BankName"" character varying(120);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""BankAccount"" character varying(40);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""BankClabe"" character varying(20);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoStoragePath"" character varying(500);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoContentType"" character varying(120);");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""ProfilePhotoOriginalFileName"" character varying(255);");

            migrationBuilder.Sql(@"UPDATE ""EmployeeDeductions"" SET ""Direction"" = 2, ""UpdatedAt"" = NOW() WHERE ""Type"" = 5 AND ""Direction"" = 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_BillingInvoiceRuns_CfdiStatus_LastSyncAt"";");

            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""CfdiUuid"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""CfdiStatus"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""CancelReasonCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""SatStatusMessage"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""PacTrackingId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""LastSyncAt"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""CancellationRequestedAt"";");
            migrationBuilder.Sql(@"ALTER TABLE ""BillingInvoiceRuns"" DROP COLUMN IF EXISTS ""CancelledAt"";");

            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""EducationLevel"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""Rfc"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""PostalCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""EmployeeNumber"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""SatContractTypeCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""SatWorkdayTypeCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""SatJobRiskCode"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""BankName"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""BankAccount"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""BankClabe"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""ProfilePhotoStoragePath"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""ProfilePhotoContentType"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""ProfilePhotoOriginalFileName"";");
        }
    }
}
