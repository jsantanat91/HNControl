using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations;

[Migration("202602240020_AddExamAnswerAttachments")]
public partial class AddExamAnswerAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
-- --------------------
-- Adjuntos por respuesta (para preguntas tipo Diagrama/Adjunto)
-- --------------------
CREATE TABLE IF NOT EXISTS public.""ExamAnswerAttachments"" (
    ""Id"" uuid NOT NULL,
    ""ExamAnswerId"" uuid NOT NULL,
    ""OriginalFileName"" character varying(255) NOT NULL DEFAULT '',
    ""ContentType"" character varying(100) NOT NULL DEFAULT '',
    ""StoragePath"" character varying(500) NOT NULL DEFAULT '',
    ""SizeBytes"" bigint NOT NULL DEFAULT 0,
    ""UploadedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_ExamAnswerAttachments"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ExamAnswerAttachments_ExamAnswers_ExamAnswerId"" FOREIGN KEY (""ExamAnswerId"")
        REFERENCES public.""ExamAnswers"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_ExamAnswerAttachments_ExamAnswerId""
    ON public.""ExamAnswerAttachments"" (""ExamAnswerId"");
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS public.""ExamAnswerAttachments"";
");
    }
}