using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTravelAdvanceAndKpiParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ViaticWeeks_UserId_WeekStartDate\";");

            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"ApprovedAdvanceAmount\" numeric(12,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"DepositedAt\" timestamp with time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"DepositedByUserId\" character varying(64) NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"FlowType\" character varying(30) NOT NULL DEFAULT 'Weekly';");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"RelatedServiceOrderId\" uuid NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"RequestedAdvanceAmount\" numeric(12,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"SettlementApprovedAt\" timestamp with time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"SettlementApprovedByUserId\" character varying(64) NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"SettlementSubmittedAt\" timestamp with time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"TripDestination\" character varying(220) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" ADD COLUMN IF NOT EXISTS \"TripPurpose\" character varying(1200) NOT NULL DEFAULT '';");

            migrationBuilder.Sql("ALTER TABLE \"PerformanceReviews\" ADD COLUMN IF NOT EXISTS \"ParticipationInTeam\" integer NOT NULL DEFAULT 3;");

            migrationBuilder.Sql("UPDATE \"ViaticWeeks\" SET \"FlowType\"='Weekly' WHERE COALESCE(\"FlowType\", '')='';");
            migrationBuilder.Sql("UPDATE \"PerformanceReviews\" SET \"ParticipationInTeam\"=3 WHERE \"ParticipationInTeam\" < 1 OR \"ParticipationInTeam\" > 5;");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ViaticWeeks_RelatedServiceOrderId\" ON \"ViaticWeeks\" (\"RelatedServiceOrderId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_ViaticWeeks_UserId_FlowType_WeekStartDate\" ON \"ViaticWeeks\" (\"UserId\", \"FlowType\", \"WeekStartDate\");");

            migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ViaticWeeks_ServiceOrders_RelatedServiceOrderId') THEN ALTER TABLE \"ViaticWeeks\" ADD CONSTRAINT \"FK_ViaticWeeks_ServiceOrders_RelatedServiceOrderId\" FOREIGN KEY (\"RelatedServiceOrderId\") REFERENCES \"ServiceOrders\" (\"Id\") ON DELETE SET NULL; END IF; END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP CONSTRAINT IF EXISTS \"FK_ViaticWeeks_ServiceOrders_RelatedServiceOrderId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ViaticWeeks_RelatedServiceOrderId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_ViaticWeeks_UserId_FlowType_WeekStartDate\";");

            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"ApprovedAdvanceAmount\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"DepositedAt\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"DepositedByUserId\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"FlowType\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"RelatedServiceOrderId\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"RequestedAdvanceAmount\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"SettlementApprovedAt\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"SettlementApprovedByUserId\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"SettlementSubmittedAt\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"TripDestination\";");
            migrationBuilder.Sql("ALTER TABLE \"ViaticWeeks\" DROP COLUMN IF EXISTS \"TripPurpose\";");

            migrationBuilder.Sql("ALTER TABLE \"PerformanceReviews\" DROP COLUMN IF EXISTS \"ParticipationInTeam\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ViaticWeeks_UserId_WeekStartDate\" ON \"ViaticWeeks\" (\"UserId\", \"WeekStartDate\");");
        }
    }
}
