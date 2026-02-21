using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCarriersInventoryPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Si existe ClientServices, la eliminamos; si no existe, no pasa nada.
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."ClientServices"') IS NOT NULL THEN
        DROP TABLE public."ClientServices";
    END IF;
END$$;
""");

            // 2) Columna en ServiceOrders (si ya existe, no falla)
            migrationBuilder.Sql("""
ALTER TABLE "ServiceOrders"
ADD COLUMN IF NOT EXISTS "ClientServiceContractId" uuid;
""");

            // -------------------------
            // ClientServiceContracts
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "ClientServiceContracts" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "ProjectId" uuid,
    "ServiceType" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Provider" character varying(120) NOT NULL,
    "AccountNumber" character varying(120) NOT NULL,
    "ContractNumber" character varying(120) NOT NULL,
    "ContractStartDate" date,
    "ContractEndDate" date,
    "Notes" character varying(2000) NOT NULL,
    "SignedContractStoragePath" character varying(500),
    "SignedContractOriginalFileName" character varying(255),
    "SignedContractContentType" character varying(100),
    "SignedContractSizeBytes" bigint,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    -- PK
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_ClientServiceContracts'
          AND conrelid='public."ClientServiceContracts"'::regclass
    ) THEN
        ALTER TABLE "ClientServiceContracts"
        ADD CONSTRAINT "PK_ClientServiceContracts" PRIMARY KEY ("Id");
    END IF;

    -- FK Clients
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ClientServiceContracts_Clients_ClientId'
          AND conrelid='public."ClientServiceContracts"'::regclass
    ) THEN
        ALTER TABLE "ClientServiceContracts"
        ADD CONSTRAINT "FK_ClientServiceContracts_Clients_ClientId"
        FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id")
        ON DELETE CASCADE;
    END IF;

    -- FK Projects
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ClientServiceContracts_Projects_ProjectId'
          AND conrelid='public."ClientServiceContracts"'::regclass
    ) THEN
        ALTER TABLE "ClientServiceContracts"
        ADD CONSTRAINT "FK_ClientServiceContracts_Projects_ProjectId"
        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id")
        ON DELETE SET NULL;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_ClientServiceContracts_ProjectId"
ON "ClientServiceContracts" ("ProjectId");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_ClientServiceContracts_ClientId_ServiceType_Name"
ON "ClientServiceContracts" ("ClientId", "ServiceType", "Name");
""");

            // -------------------------
            // InternetCarriers
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "InternetCarriers" (
    "Id" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "SupportPhone" character varying(40) NOT NULL,
    "SupportEmail" character varying(120) NOT NULL,
    "SupportPortalUrl" character varying(400) NOT NULL,
    "Notes" character varying(2000) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_InternetCarriers'
          AND conrelid='public."InternetCarriers"'::regclass
    ) THEN
        ALTER TABLE "InternetCarriers"
        ADD CONSTRAINT "PK_InternetCarriers" PRIMARY KEY ("Id");
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InternetCarriers_Name"
ON "InternetCarriers" ("Name");
""");

            // -------------------------
            // InventoryItems
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "InventoryItems" (
    "Id" uuid NOT NULL,
    "Sku" character varying(60) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Category" character varying(100) NOT NULL,
    "Unit" character varying(40) NOT NULL,
    "IsConsumable" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "QuantityOnHand" numeric(18,3) NOT NULL,
    "ReorderLevel" numeric(18,3) NOT NULL,
    "Notes" character varying(2000) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_InventoryItems'
          AND conrelid='public."InventoryItems"'::regclass
    ) THEN
        ALTER TABLE "InventoryItems"
        ADD CONSTRAINT "PK_InventoryItems" PRIMARY KEY ("Id");
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_InventoryItems_Sku"
ON "InventoryItems" ("Sku");
""");

            // -------------------------
            // PermissionRoles
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "PermissionRoles" (
    "Id" uuid NOT NULL,
    "Name" character varying(80) NOT NULL,
    "Description" character varying(400) NOT NULL,
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_PermissionRoles'
          AND conrelid='public."PermissionRoles"'::regclass
    ) THEN
        ALTER TABLE "PermissionRoles"
        ADD CONSTRAINT "PK_PermissionRoles" PRIMARY KEY ("Id");
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PermissionRoles_Name"
ON "PermissionRoles" ("Name");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_PermissionRoles_IsDefault"
ON "PermissionRoles" ("IsDefault");
""");

            // -------------------------
            // ClientCarrierServices
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "ClientCarrierServices" (
    "Id" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "CarrierId" uuid NOT NULL,
    "ServiceLabel" character varying(140) NOT NULL,
    "Plan" character varying(140) NOT NULL,
    "AccountNumber" character varying(120) NOT NULL,
    "ContractNumber" character varying(120) NOT NULL,
    "CircuitId" character varying(120) NOT NULL,
    "ServiceAddress" character varying(200) NOT NULL,
    "IpInfo" character varying(200) NOT NULL,
    "SupportPhoneOverride" character varying(40) NOT NULL,
    "Notes" character varying(2000) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_ClientCarrierServices'
          AND conrelid='public."ClientCarrierServices"'::regclass
    ) THEN
        ALTER TABLE "ClientCarrierServices"
        ADD CONSTRAINT "PK_ClientCarrierServices" PRIMARY KEY ("Id");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ClientCarrierServices_Clients_ClientId'
          AND conrelid='public."ClientCarrierServices"'::regclass
    ) THEN
        ALTER TABLE "ClientCarrierServices"
        ADD CONSTRAINT "FK_ClientCarrierServices_Clients_ClientId"
        FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id")
        ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ClientCarrierServices_InternetCarriers_CarrierId'
          AND conrelid='public."ClientCarrierServices"'::regclass
    ) THEN
        ALTER TABLE "ClientCarrierServices"
        ADD CONSTRAINT "FK_ClientCarrierServices_InternetCarriers_CarrierId"
        FOREIGN KEY ("CarrierId") REFERENCES "InternetCarriers" ("Id")
        ON DELETE RESTRICT;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_ClientCarrierServices_CarrierId"
ON "ClientCarrierServices" ("CarrierId");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_ClientCarrierServices_ClientId_CarrierId"
ON "ClientCarrierServices" ("ClientId", "CarrierId");
""");

            // -------------------------
            // InventoryMovements
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "InventoryMovements" (
    "Id" uuid NOT NULL,
    "ItemId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "ProjectId" uuid,
    "AssignedClientId" uuid,
    "SerialNumber" character varying(120) NOT NULL,
    "Reference" character varying(120) NOT NULL,
    "RequestedByUserId" character varying(64) NOT NULL,
    "RequestedByName" character varying(200) NOT NULL,
    "ResponsibleUserId" character varying(64) NOT NULL,
    "ResponsibleName" character varying(200) NOT NULL,
    "Status" integer NOT NULL,
    "ApprovedByUserId" character varying(64),
    "ApprovedByName" character varying(200),
    "ApprovedAt" timestamp with time zone,
    "RequestedAt" timestamp with time zone NOT NULL,
    "Notes" character varying(2000) NOT NULL,
    "AdminNote" character varying(2000) NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_InventoryMovements'
          AND conrelid='public."InventoryMovements"'::regclass
    ) THEN
        ALTER TABLE "InventoryMovements"
        ADD CONSTRAINT "PK_InventoryMovements" PRIMARY KEY ("Id");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_InventoryMovements_InventoryItems_ItemId'
          AND conrelid='public."InventoryMovements"'::regclass
    ) THEN
        ALTER TABLE "InventoryMovements"
        ADD CONSTRAINT "FK_InventoryMovements_InventoryItems_ItemId"
        FOREIGN KEY ("ItemId") REFERENCES "InventoryItems" ("Id")
        ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_InventoryMovements_Clients_AssignedClientId'
          AND conrelid='public."InventoryMovements"'::regclass
    ) THEN
        ALTER TABLE "InventoryMovements"
        ADD CONSTRAINT "FK_InventoryMovements_Clients_AssignedClientId"
        FOREIGN KEY ("AssignedClientId") REFERENCES "Clients" ("Id")
        ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_InventoryMovements_Projects_ProjectId'
          AND conrelid='public."InventoryMovements"'::regclass
    ) THEN
        ALTER TABLE "InventoryMovements"
        ADD CONSTRAINT "FK_InventoryMovements_Projects_ProjectId"
        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id")
        ON DELETE SET NULL;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_InventoryMovements_AssignedClientId"
ON "InventoryMovements" ("AssignedClientId");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_InventoryMovements_ItemId"
ON "InventoryMovements" ("ItemId");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_InventoryMovements_ProjectId"
ON "InventoryMovements" ("ProjectId");
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_InventoryMovements_Status_RequestedAt"
ON "InventoryMovements" ("Status", "RequestedAt");
""");

            // -------------------------
            // PermissionRoleModules
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "PermissionRoleModules" (
    "Id" uuid NOT NULL,
    "PermissionRoleId" uuid NOT NULL,
    "ModuleKey" character varying(60) NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_PermissionRoleModules'
          AND conrelid='public."PermissionRoleModules"'::regclass
    ) THEN
        ALTER TABLE "PermissionRoleModules"
        ADD CONSTRAINT "PK_PermissionRoleModules" PRIMARY KEY ("Id");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_PermissionRoleModules_PermissionRoles_PermissionRoleId'
          AND conrelid='public."PermissionRoleModules"'::regclass
    ) THEN
        ALTER TABLE "PermissionRoleModules"
        ADD CONSTRAINT "FK_PermissionRoleModules_PermissionRoles_PermissionRoleId"
        FOREIGN KEY ("PermissionRoleId") REFERENCES "PermissionRoles" ("Id")
        ON DELETE CASCADE;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PermissionRoleModules_PermissionRoleId_ModuleKey"
ON "PermissionRoleModules" ("PermissionRoleId", "ModuleKey");
""");

            // -------------------------
            // UserPermissionRoles
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "UserPermissionRoles" (
    "UserId" character varying(64) NOT NULL,
    "PermissionRoleId" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    "AssignedByUserId" character varying(64)
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_UserPermissionRoles'
          AND conrelid='public."UserPermissionRoles"'::regclass
    ) THEN
        ALTER TABLE "UserPermissionRoles"
        ADD CONSTRAINT "PK_UserPermissionRoles" PRIMARY KEY ("UserId");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_UserPermissionRoles_PermissionRoles_PermissionRoleId'
          AND conrelid='public."UserPermissionRoles"'::regclass
    ) THEN
        ALTER TABLE "UserPermissionRoles"
        ADD CONSTRAINT "FK_UserPermissionRoles_PermissionRoles_PermissionRoleId"
        FOREIGN KEY ("PermissionRoleId") REFERENCES "PermissionRoles" ("Id")
        ON DELETE RESTRICT;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_UserPermissionRoles_PermissionRoleId"
ON "UserPermissionRoles" ("PermissionRoleId");
""");

            // -------------------------
            // ClientCarrierNotes
            // -------------------------
            migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "ClientCarrierNotes" (
    "Id" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "NoteType" integer NOT NULL,
    "TicketNumber" character varying(120) NOT NULL,
    "Message" character varying(3000) NOT NULL,
    "CreatedByUserId" character varying(64) NOT NULL,
    "CreatedByName" character varying(200) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='PK_ClientCarrierNotes'
          AND conrelid='public."ClientCarrierNotes"'::regclass
    ) THEN
        ALTER TABLE "ClientCarrierNotes"
        ADD CONSTRAINT "PK_ClientCarrierNotes" PRIMARY KEY ("Id");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ClientCarrierNotes_ClientCarrierServices_ServiceId'
          AND conrelid='public."ClientCarrierNotes"'::regclass
    ) THEN
        ALTER TABLE "ClientCarrierNotes"
        ADD CONSTRAINT "FK_ClientCarrierNotes_ClientCarrierServices_ServiceId"
        FOREIGN KEY ("ServiceId") REFERENCES "ClientCarrierServices" ("Id")
        ON DELETE CASCADE;
    END IF;
END$$;
""");

            migrationBuilder.Sql("""
CREATE INDEX IF NOT EXISTS "IX_ClientCarrierNotes_ServiceId_CreatedAt"
ON "ClientCarrierNotes" ("ServiceId", "CreatedAt");
""");

            // -------------------------
            // Index + FK en ServiceOrders hacia ClientServiceContracts (si no existe)
            // -------------------------
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."ServiceOrders"') IS NOT NULL
       AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema='public' AND table_name='ServiceOrders' AND column_name='ClientServiceContractId'
       ) THEN

        -- Index
        IF to_regclass('public."IX_ServiceOrders_ClientServiceContractId"') IS NULL THEN
            CREATE INDEX "IX_ServiceOrders_ClientServiceContractId" ON "ServiceOrders" ("ClientServiceContractId");
        END IF;

        -- FK
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conname='FK_ServiceOrders_ClientServiceContracts_ClientServiceContractId'
              AND conrelid='public."ServiceOrders"'::regclass
        ) THEN
            ALTER TABLE "ServiceOrders"
            ADD CONSTRAINT "FK_ServiceOrders_ClientServiceContracts_ClientServiceContractId"
            FOREIGN KEY ("ClientServiceContractId") REFERENCES "ClientServiceContracts" ("Id")
            ON DELETE SET NULL;
        END IF;

    END IF;
END$$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down idempotente (por si alguien lo corre)
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname='FK_ServiceOrders_ClientServiceContracts_ClientServiceContractId'
          AND conrelid='public."ServiceOrders"'::regclass
    ) THEN
        ALTER TABLE "ServiceOrders"
        DROP CONSTRAINT "FK_ServiceOrders_ClientServiceContracts_ClientServiceContractId";
    END IF;

    IF to_regclass('public."IX_ServiceOrders_ClientServiceContractId"') IS NOT NULL THEN
        DROP INDEX "IX_ServiceOrders_ClientServiceContractId";
    END IF;
END$$;
""");

            migrationBuilder.Sql("""DROP TABLE IF EXISTS "ClientCarrierNotes";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "InventoryMovements";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "PermissionRoleModules";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "UserPermissionRoles";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "ClientCarrierServices";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "InventoryItems";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "PermissionRoles";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "InternetCarriers";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "ClientServiceContracts";""");

            migrationBuilder.Sql("""
ALTER TABLE "ServiceOrders"
DROP COLUMN IF EXISTS "ClientServiceContractId";
""");

            migrationBuilder.Sql("""
DO $$
BEGIN
    IF to_regclass('public."ClientServices"') IS NULL THEN
        CREATE TABLE public."ClientServices" (
            "ClientId" uuid NOT NULL,
            "ServiceType" integer NOT NULL,
            "Notes" character varying(200) NULL,
            CONSTRAINT "PK_ClientServices" PRIMARY KEY ("ClientId", "ServiceType"),
            CONSTRAINT "FK_ClientServices_Clients_ClientId"
                FOREIGN KEY ("ClientId") REFERENCES public."Clients" ("Id") ON DELETE CASCADE
        );
    END IF;
END$$;
""");
        }
    }
}