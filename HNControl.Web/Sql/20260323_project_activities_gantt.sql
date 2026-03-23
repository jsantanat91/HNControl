-- 2026-03-23: Actividades de proyecto para Gantt real
BEGIN;

CREATE TABLE IF NOT EXISTS "ProjectActivities" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "AssignedToName" character varying(200) NOT NULL DEFAULT '',
    "Description" character varying(1000) NOT NULL DEFAULT '',
    "PlannedDays" integer NOT NULL DEFAULT 1,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_ProjectActivities" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProjectActivities_Projects_ProjectId"
        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ProjectActivities_ProjectId_SortOrder"
    ON "ProjectActivities" ("ProjectId", "SortOrder");

COMMIT;
