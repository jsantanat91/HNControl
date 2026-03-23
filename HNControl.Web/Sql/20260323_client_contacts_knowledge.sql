-- HNControl - Migracion manual 2026-03-23
-- Ejecutar en PostgreSQL sobre la misma base usada por HNControl.Web

BEGIN;

CREATE TABLE IF NOT EXISTS "ClientContacts" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "Name" character varying(180) NOT NULL,
    "Email" character varying(256) NOT NULL DEFAULT '',
    "Phone" character varying(60) NOT NULL DEFAULT '',
    "Role" character varying(120) NOT NULL DEFAULT '',
    "IsPrimary" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ClientContacts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ClientContacts_Clients_ClientId"
        FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ClientContacts_ClientId_Email"
    ON "ClientContacts" ("ClientId", "Email");

CREATE INDEX IF NOT EXISTS "IX_ClientContacts_ClientId_IsPrimary"
    ON "ClientContacts" ("ClientId", "IsPrimary");

CREATE INDEX IF NOT EXISTS "IX_ClientContacts_ClientId_Name"
    ON "ClientContacts" ("ClientId", "Name");

ALTER TABLE "KnowledgeLinks"
    ADD COLUMN IF NOT EXISTS "ClientId" uuid;

ALTER TABLE "KnowledgeLinks"
    ADD COLUMN IF NOT EXISTS "ClientServiceContractId" uuid;

CREATE INDEX IF NOT EXISTS "IX_KnowledgeLinks_ClientId"
    ON "KnowledgeLinks" ("ClientId");

CREATE INDEX IF NOT EXISTS "IX_KnowledgeLinks_ClientServiceContractId"
    ON "KnowledgeLinks" ("ClientServiceContractId");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_KnowledgeLinks_Clients_ClientId'
    ) THEN
        ALTER TABLE "KnowledgeLinks"
            ADD CONSTRAINT "FK_KnowledgeLinks_Clients_ClientId"
            FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContractId'
    ) THEN
        ALTER TABLE "KnowledgeLinks"
            ADD CONSTRAINT "FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContractId"
            FOREIGN KEY ("ClientServiceContractId") REFERENCES "ClientServiceContracts" ("Id")
            ON DELETE SET NULL;
    END IF;
END $$;

COMMIT;
