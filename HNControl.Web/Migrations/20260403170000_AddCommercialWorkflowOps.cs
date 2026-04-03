using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260403170000_AddCommercialWorkflowOps")]
    public partial class AddCommercialWorkflowOps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE \""Clients\"" ADD COLUMN IF NOT EXISTS \""BillingEmail\"" character varying(256);");
            migrationBuilder.Sql(@"ALTER TABLE \""Clients\"" ADD COLUMN IF NOT EXISTS \""FiscalZipCode\"" character varying(10);");
            migrationBuilder.Sql(@"ALTER TABLE \""Clients\"" ADD COLUMN IF NOT EXISTS \""FiscalRegimeCode\"" character varying(4);");
            migrationBuilder.Sql(@"ALTER TABLE \""Clients\"" ADD COLUMN IF NOT EXISTS \""CfdiUseCodeDefault\"" character varying(4);");

            migrationBuilder.Sql(@"ALTER TABLE \""SalesOpportunities\"" ADD COLUMN IF NOT EXISTS \""WorkflowStage\"" integer NOT NULL DEFAULT 1;");
            migrationBuilder.Sql(@"ALTER TABLE \""SalesOpportunities\"" ADD COLUMN IF NOT EXISTS \""StageChangedAt\"" timestamp with time zone NOT NULL DEFAULT now();");
            migrationBuilder.Sql(@"ALTER TABLE \""SalesOpportunities\"" ADD COLUMN IF NOT EXISTS \""StageDueAt\"" timestamp with time zone NULL;");
            migrationBuilder.Sql(@"ALTER TABLE \""SalesOpportunities\"" ADD COLUMN IF NOT EXISTS \""OwnerUserId\"" character varying(64) NULL;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS \""IX_SalesOpportunities_WorkflowStage_StageDueAt\"" ON \""SalesOpportunities\"" (\""WorkflowStage\"", \""StageDueAt\"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS \""PermissionRoleActions\"" (
    \""Id\"" uuid NOT NULL,
    \""PermissionRoleId\"" uuid NOT NULL,
    \""ActionKey\"" character varying(80) NOT NULL,
    CONSTRAINT \""PK_PermissionRoleActions\"" PRIMARY KEY (\""Id\""),
    CONSTRAINT \""FK_PermissionRoleActions_PermissionRoles_PermissionRoleId\""
        FOREIGN KEY (\""PermissionRoleId\"") REFERENCES \""PermissionRoles\"" (\""Id\"") ON DELETE CASCADE
);");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS \""IX_PermissionRoleActions_PermissionRoleId_ActionKey\"" ON \""PermissionRoleActions\"" (\""PermissionRoleId\"", \""ActionKey\"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS \""SalesAuditLogs\"" (
    \""Id\"" uuid NOT NULL,
    \""SalesOpportunityId\"" uuid NOT NULL,
    \""EventType\"" character varying(80) NOT NULL,
    \""UserId\"" character varying(64) NULL,
    \""UserName\"" character varying(180) NOT NULL,
    \""PreviousStage\"" integer NULL,
    \""NewStage\"" integer NULL,
    \""Details\"" character varying(1400) NOT NULL,
    \""CreatedAt\"" timestamp with time zone NOT NULL,
    CONSTRAINT \""PK_SalesAuditLogs\"" PRIMARY KEY (\""Id\""),
    CONSTRAINT \""FK_SalesAuditLogs_SalesOpportunities_SalesOpportunityId\""
        FOREIGN KEY (\""SalesOpportunityId\"") REFERENCES \""SalesOpportunities\"" (\""Id\"") ON DELETE CASCADE
);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS \""IX_SalesAuditLogs_SalesOpportunityId_CreatedAt\"" ON \""SalesAuditLogs\"" (\""SalesOpportunityId\"", \""CreatedAt\"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS \""BillingAuditLogs\"" (
    \""Id\"" uuid NOT NULL,
    \""BillingPlanId\"" uuid NOT NULL,
    \""EventType\"" character varying(80) NOT NULL,
    \""UserId\"" character varying(64) NULL,
    \""UserName\"" character varying(180) NOT NULL,
    \""Details\"" character varying(1400) NOT NULL,
    \""CreatedAt\"" timestamp with time zone NOT NULL,
    CONSTRAINT \""PK_BillingAuditLogs\"" PRIMARY KEY (\""Id\""),
    CONSTRAINT \""FK_BillingAuditLogs_BillingInvoicePlans_BillingPlanId\""
        FOREIGN KEY (\""BillingPlanId\"") REFERENCES \""BillingInvoicePlans\"" (\""Id\"") ON DELETE CASCADE
);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS \""IX_BillingAuditLogs_BillingPlanId_CreatedAt\"" ON \""BillingAuditLogs\"" (\""BillingPlanId\"", \""CreatedAt\"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS \""EventEmailTemplates\"" (
    \""Id\"" uuid NOT NULL,
    \""EventKey\"" character varying(80) NOT NULL,
    \""SubjectTemplate\"" character varying(220) NOT NULL,
    \""BodyTemplate\"" character varying(12000) NOT NULL,
    \""IsActive\"" boolean NOT NULL,
    \""UpdatedAt\"" timestamp with time zone NOT NULL,
    CONSTRAINT \""PK_EventEmailTemplates\"" PRIMARY KEY (\""Id\"")
);");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS \""IX_EventEmailTemplates_EventKey\"" ON \""EventEmailTemplates\"" (\""EventKey\"");");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS \""AutomationReminderLogs\"" (
    \""Id\"" uuid NOT NULL,
    \""ReminderType\"" character varying(80) NOT NULL,
    \""LogDate\"" date NOT NULL,
    \""SentAt\"" timestamp with time zone NOT NULL,
    CONSTRAINT \""PK_AutomationReminderLogs\"" PRIMARY KEY (\""Id\"")
);");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS \""IX_AutomationReminderLogs_ReminderType_LogDate\"" ON \""AutomationReminderLogs\"" (\""ReminderType\"", \""LogDate\"");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS \""AutomationReminderLogs\"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS \""EventEmailTemplates\"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS \""BillingAuditLogs\"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS \""SalesAuditLogs\"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS \""PermissionRoleActions\"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS \""IX_SalesOpportunities_WorkflowStage_StageDueAt\"";");
        }
    }
}
