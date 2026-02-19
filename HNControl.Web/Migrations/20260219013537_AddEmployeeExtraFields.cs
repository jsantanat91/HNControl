using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===============================
            // EmployeeProfiles: columnas nuevas (idempotente)
            // ===============================
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""Address"" character varying(400) NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""BirthDate"" date NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""Curp"" character varying(18) NOT NULL DEFAULT '';");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" ADD COLUMN IF NOT EXISTS ""HireDate"" date NULL;");

            // Normaliza por si existían como NULL
            migrationBuilder.Sql(@"UPDATE ""EmployeeProfiles"" SET ""Address"" = '' WHERE ""Address"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""EmployeeProfiles"" SET ""Curp"" = '' WHERE ""Curp"" IS NULL;");

            // ===============================
            // Eval360 tables (idempotente)
            // ===============================
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Campaigns"" (
    ""Id"" uuid NOT NULL,
    ""Title"" character varying(200) NOT NULL,
    ""Description"" character varying(800) NOT NULL,
    ""PeriodStart"" date NULL,
    ""PeriodEnd"" date NULL,
    ""Status"" integer NOT NULL,
    ""AllowSelf"" boolean NOT NULL,
    ""ResultsVisibleToEmployee"" boolean NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_Eval360Campaigns"" PRIMARY KEY (""Id"")
);");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Competencies"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""SortOrder"" integer NOT NULL,
    ""IsActive"" boolean NOT NULL,
    CONSTRAINT ""PK_Eval360Competencies"" PRIMARY KEY (""Id"")
);");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Assignments"" (
    ""Id"" uuid NOT NULL,
    ""CampaignId"" uuid NOT NULL,
    ""EvaluatorUserId"" character varying(64) NOT NULL,
    ""SubjectUserId"" character varying(64) NOT NULL,
    ""IsSelf"" boolean NOT NULL,
    ""Status"" integer NOT NULL,
    ""StartedAt"" timestamp with time zone NULL,
    ""SubmittedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_Eval360Assignments"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Eval360Assignments_Eval360Campaigns_CampaignId""
        FOREIGN KEY (""CampaignId"") REFERENCES ""Eval360Campaigns"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Questions"" (
    ""Id"" uuid NOT NULL,
    ""CompetencyId"" uuid NOT NULL,
    ""Text"" character varying(600) NOT NULL,
    ""SortOrder"" integer NOT NULL,
    ""IsActive"" boolean NOT NULL,
    CONSTRAINT ""PK_Eval360Questions"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Eval360Questions_Eval360Competencies_CompetencyId""
        FOREIGN KEY (""CompetencyId"") REFERENCES ""Eval360Competencies"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Comments"" (
    ""Id"" uuid NOT NULL,
    ""AssignmentId"" uuid NOT NULL,
    ""CompetencyId"" uuid NOT NULL,
    ""CommentText"" character varying(2000) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_Eval360Comments"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Eval360Comments_Eval360Assignments_AssignmentId""
        FOREIGN KEY (""AssignmentId"") REFERENCES ""Eval360Assignments"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_Eval360Comments_Eval360Competencies_CompetencyId""
        FOREIGN KEY (""CompetencyId"") REFERENCES ""Eval360Competencies"" (""Id"") ON DELETE CASCADE
);");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Eval360Answers"" (
    ""Id"" uuid NOT NULL,
    ""AssignmentId"" uuid NOT NULL,
    ""QuestionId"" uuid NOT NULL,
    ""Score"" integer NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_Eval360Answers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Eval360Answers_Eval360Assignments_AssignmentId""
        FOREIGN KEY (""AssignmentId"") REFERENCES ""Eval360Assignments"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_Eval360Answers_Eval360Questions_QuestionId""
        FOREIGN KEY (""QuestionId"") REFERENCES ""Eval360Questions"" (""Id"") ON DELETE CASCADE
);");

            // ===============================
            // Índices (idempotente)
            // ===============================
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Eval360Answers_AssignmentId_QuestionId"" ON ""Eval360Answers"" (""AssignmentId"", ""QuestionId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Eval360Answers_QuestionId"" ON ""Eval360Answers"" (""QuestionId"");");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Eval360Assignments_CampaignId_EvaluatorUserId_SubjectUserId"" ON ""Eval360Assignments"" (""CampaignId"", ""EvaluatorUserId"", ""SubjectUserId"");");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Eval360Comments_AssignmentId_CompetencyId"" ON ""Eval360Comments"" (""AssignmentId"", ""CompetencyId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Eval360Comments_CompetencyId"" ON ""Eval360Comments"" (""CompetencyId"");");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Eval360Competencies_SortOrder"" ON ""Eval360Competencies"" (""SortOrder"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Eval360Questions_CompetencyId_SortOrder"" ON ""Eval360Questions"" (""CompetencyId"", ""SortOrder"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropear en orden inverso, con IF EXISTS para no reventar
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Answers"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Comments"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Questions"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Assignments"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Competencies"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Eval360Campaigns"";");

            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""Address"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""BirthDate"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""Curp"";");
            migrationBuilder.Sql(@"ALTER TABLE ""EmployeeProfiles"" DROP COLUMN IF EXISTS ""HireDate"";");
        }
    }
}
