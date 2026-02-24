using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations;

[Migration("202602232355_AddLeavesAndExams")]
public partial class AddLeavesAndExams : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
-- --------------------
-- EmployeeProfiles: vacaciones
-- --------------------
ALTER TABLE IF EXISTS public.""EmployeeProfiles""
    ADD COLUMN IF NOT EXISTS ""VacationAllowanceDays"" integer NOT NULL DEFAULT 12;

-- --------------------
-- Vacaciones e incidencias
-- --------------------
CREATE TABLE IF NOT EXISTS public.""LeaveRequests"" (
    ""Id"" uuid NOT NULL,
    ""UserId"" character varying(64) NOT NULL,
    ""Type"" integer NOT NULL,
    ""StartDate"" date NOT NULL,
    ""EndDate"" date NOT NULL,
    ""TotalDays"" integer NOT NULL,
    ""Reason"" character varying(1200) NOT NULL DEFAULT '',
    ""Status"" integer NOT NULL DEFAULT 0,
    ""RequestedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""ReviewedAt"" timestamp with time zone NULL,
    ""ReviewedByUserId"" character varying(64) NULL,
    ""AdminComment"" character varying(600) NOT NULL DEFAULT '',
    ""CreatedByAdmin"" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ""PK_LeaveRequests"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_LeaveRequests_EmployeeProfiles_UserId"" FOREIGN KEY (""UserId"")
        REFERENCES public.""EmployeeProfiles"" (""UserId"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_LeaveRequests_User_Status_Start""
    ON public.""LeaveRequests"" (""UserId"", ""Status"", ""StartDate"");

CREATE INDEX IF NOT EXISTS ""IX_LeaveRequests_Type""
    ON public.""LeaveRequests"" (""Type"");

CREATE TABLE IF NOT EXISTS public.""LeaveEvidences"" (
    ""Id"" uuid NOT NULL,
    ""LeaveRequestId"" uuid NOT NULL,
    ""OriginalFileName"" character varying(255) NOT NULL DEFAULT '',
    ""ContentType"" character varying(100) NOT NULL DEFAULT '',
    ""StoragePath"" character varying(500) NOT NULL DEFAULT '',
    ""SizeBytes"" bigint NOT NULL DEFAULT 0,
    ""UploadedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_LeaveEvidences"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_LeaveEvidences_LeaveRequests_LeaveRequestId"" FOREIGN KEY (""LeaveRequestId"")
        REFERENCES public.""LeaveRequests"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_LeaveEvidences_LeaveRequestId""
    ON public.""LeaveEvidences"" (""LeaveRequestId"");

-- --------------------
-- Exámenes
-- --------------------
CREATE TABLE IF NOT EXISTS public.""Exams"" (
    ""Id"" uuid NOT NULL,
    ""Title"" character varying(200) NOT NULL,
    ""Description"" character varying(2000) NOT NULL DEFAULT '',
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""TimeLimitMinutes"" integer NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""CreatedByUserId"" character varying(64) NULL,
    CONSTRAINT ""PK_Exams"" PRIMARY KEY (""Id"")
);

CREATE INDEX IF NOT EXISTS ""IX_Exams_IsActive""
    ON public.""Exams"" (""IsActive"");

CREATE TABLE IF NOT EXISTS public.""ExamQuestions"" (
    ""Id"" uuid NOT NULL,
    ""ExamId"" uuid NOT NULL,
    ""Ordinal"" integer NOT NULL,
    ""Type"" integer NOT NULL,
    ""Text"" character varying(2000) NOT NULL,
    ""Points"" numeric(12,2) NOT NULL DEFAULT 1,
    ""IsRequired"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_ExamQuestions"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamQuestions_Exams_ExamId"" FOREIGN KEY (""ExamId"")
        REFERENCES public.""Exams"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_ExamQuestions_Exam_Ord""
    ON public.""ExamQuestions"" (""ExamId"", ""Ordinal"");

CREATE TABLE IF NOT EXISTS public.""ExamChoices"" (
    ""Id"" uuid NOT NULL,
    ""QuestionId"" uuid NOT NULL,
    ""Ordinal"" integer NOT NULL DEFAULT 1,
    ""Text"" character varying(1000) NOT NULL,
    ""IsCorrect"" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT ""PK_ExamChoices"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamChoices_ExamQuestions_QuestionId"" FOREIGN KEY (""QuestionId"")
        REFERENCES public.""ExamQuestions"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_ExamChoices_Question_Ord""
    ON public.""ExamChoices"" (""QuestionId"", ""Ordinal"");

CREATE TABLE IF NOT EXISTS public.""ExamAssignments"" (
    ""Id"" uuid NOT NULL,
    ""ExamId"" uuid NOT NULL,
    ""UserId"" character varying(64) NOT NULL,
    ""Status"" integer NOT NULL DEFAULT 0,
    ""AssignedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""DueAt"" timestamp with time zone NULL,
    ""StartedAt"" timestamp with time zone NULL,
    ""SubmittedAt"" timestamp with time zone NULL,
    ""GradedAt"" timestamp with time zone NULL,
    ""Score"" numeric(12,2) NOT NULL DEFAULT 0,
    ""MaxScore"" numeric(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT ""PK_ExamAssignments"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamAssignments_Exams_ExamId"" FOREIGN KEY (""ExamId"")
        REFERENCES public.""Exams"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ExamAssignments_EmployeeProfiles_UserId"" FOREIGN KEY (""UserId"")
        REFERENCES public.""EmployeeProfiles"" (""UserId"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_ExamAssignments_Exam_User_Status""
    ON public.""ExamAssignments"" (""ExamId"", ""UserId"", ""Status"");

CREATE TABLE IF NOT EXISTS public.""ExamAnswers"" (
    ""Id"" uuid NOT NULL,
    ""AssignmentId"" uuid NOT NULL,
    ""QuestionId"" uuid NOT NULL,
    ""TextAnswer"" text NOT NULL DEFAULT '',
    ""AutoScore"" numeric(12,2) NOT NULL DEFAULT 0,
    ""ManualScore"" numeric(12,2) NOT NULL DEFAULT 0,
    ""Comment"" character varying(1000) NOT NULL DEFAULT '',
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_ExamAnswers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamAnswers_ExamAssignments_AssignmentId"" FOREIGN KEY (""AssignmentId"")
        REFERENCES public.""ExamAssignments"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ExamAnswers_ExamQuestions_QuestionId"" FOREIGN KEY (""QuestionId"")
        REFERENCES public.""ExamQuestions"" (""Id"") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ExamAnswers_Assignment_Question""
    ON public.""ExamAnswers"" (""AssignmentId"", ""QuestionId"");

CREATE TABLE IF NOT EXISTS public.""ExamAnswerChoices"" (
    ""Id"" uuid NOT NULL,
    ""ExamAnswerId"" uuid NOT NULL,
    ""ChoiceId"" uuid NOT NULL,
    CONSTRAINT ""PK_ExamAnswerChoices"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamAnswerChoices_ExamAnswers_ExamAnswerId"" FOREIGN KEY (""ExamAnswerId"")
        REFERENCES public.""ExamAnswers"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ExamAnswerChoices_ExamChoices_ChoiceId"" FOREIGN KEY (""ChoiceId"")
        REFERENCES public.""ExamChoices"" (""Id"") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ExamAnswerChoices_Answer_Choice""
    ON public.""ExamAnswerChoices"" (""ExamAnswerId"", ""ChoiceId"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS public.""ExamAnswerChoices"";
DROP TABLE IF EXISTS public.""ExamAnswers"";
DROP TABLE IF EXISTS public.""ExamAssignments"";
DROP TABLE IF EXISTS public.""ExamChoices"";
DROP TABLE IF EXISTS public.""ExamQuestions"";
DROP TABLE IF EXISTS public.""Exams"";

DROP TABLE IF EXISTS public.""LeaveEvidences"";
DROP TABLE IF EXISTS public.""LeaveRequests"";

ALTER TABLE IF EXISTS public.""EmployeeProfiles"" DROP COLUMN IF EXISTS ""VacationAllowanceDays"";
");
    }
}