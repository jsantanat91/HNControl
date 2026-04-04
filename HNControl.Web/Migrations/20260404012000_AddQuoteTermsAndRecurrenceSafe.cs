using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddQuoteTermsAndRecurrenceSafe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequests' AND column_name='GeneralTerms'
    ) THEN
        ALTER TABLE "QuoteRequests" ADD COLUMN "GeneralTerms" character varying(4000);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequests' AND column_name='ContractTermMonths'
    ) THEN
        ALTER TABLE "QuoteRequests" ADD COLUMN "ContractTermMonths" integer;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequestLines' AND column_name='Recurrence'
    ) THEN
        ALTER TABLE "QuoteRequestLines" ADD COLUMN "Recurrence" character varying(30);
    END IF;
END $$;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequestLines' AND column_name='Recurrence'
    ) THEN
        ALTER TABLE "QuoteRequestLines" DROP COLUMN "Recurrence";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequests' AND column_name='ContractTermMonths'
    ) THEN
        ALTER TABLE "QuoteRequests" DROP COLUMN "ContractTermMonths";
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='QuoteRequests' AND column_name='GeneralTerms'
    ) THEN
        ALTER TABLE "QuoteRequests" DROP COLUMN "GeneralTerms";
    END IF;
END $$;
""");
        }
    }
}
