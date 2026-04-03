using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260403220000_AddPermissionAuditAndPresets")]
    public partial class AddPermissionAuditAndPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""PermissionAuditLogs"" (
    ""Id"" uuid NOT NULL,
    ""EventType"" character varying(80) NOT NULL,
    ""PermissionRoleId"" uuid NULL,
    ""RoleName"" character varying(80) NOT NULL,
    ""ActorUserId"" character varying(64) NULL,
    ""ActorName"" character varying(180) NOT NULL,
    ""Details"" character varying(1600) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_PermissionAuditLogs"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_PermissionAuditLogs_PermissionRoles_PermissionRoleId""
        FOREIGN KEY (""PermissionRoleId"") REFERENCES ""PermissionRoles"" (""Id"") ON DELETE SET NULL
);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_PermissionAuditLogs_CreatedAt"" ON ""PermissionAuditLogs"" (""CreatedAt"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_PermissionAuditLogs_PermissionRoleId"" ON ""PermissionAuditLogs"" (""PermissionRoleId"");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""PermissionAuditLogs"";");
        }
    }
}
