-- Inventario Servicios: tablas independientes; no modifica Carriers ni sus registros.
-- Ejecutar en la base de HN Control con su propietario. Es transaccional y repetible.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL search_path = public;
-- Si ejecutas como postgres u otro propietario distinto al usuario web,
-- descomenta e indica el rol EXISTENTE de la conexión de HN Control:
-- SET LOCAL hncontrol.application_role = 'ROL_EXISTENTE_DE_HNCONTROL';

DO $preflight$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = current_user AND rolsuper)
        AND nullif(current_setting('hncontrol.application_role', true), '') IS NULL THEN
        RAISE EXCEPTION 'Indique el rol existente de la aplicación en SET LOCAL hncontrol.application_role antes de ejecutar como postgres o superusuario. No se ha creado ninguna tabla.';
    END IF;
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL OR to_regclass('public."Clients"') IS NULL THEN
        RAISE EXCEPTION 'Base incorrecta o esquema base incompleto. Seleccione la base de HN Control.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261023000100_AddEngineeringNotificationEmails') THEN
        RAISE EXCEPTION 'Falta la migración previa de Ingeniería. Actualice ese esquema antes de continuar.';
    END IF;
END $preflight$;
DO $inventory$
BEGIN
IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108000100_AddServiceInventory') THEN
CREATE TABLE "ServiceInventoryAccounts" (
    "Id" uuid NOT NULL,
    "CarrierKey" character varying(32) NOT NULL,
    "OtherCarrierName" character varying(120) NOT NULL,
    "Label" character varying(140) NOT NULL,
    "Email" character varying(256) NOT NULL,
    "AccountNumber" character varying(120) NOT NULL,
    "PasswordProtected" text NOT NULL,
    "Notes" character varying(2000) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_ServiceInventoryAccounts" PRIMARY KEY ("Id")
);
CREATE INDEX "IX_ServiceInventoryAccounts_CarrierKey_IsActive" ON "ServiceInventoryAccounts" ("CarrierKey", "IsActive");

CREATE TABLE "ServiceInventoryItems" (
    "Id" uuid NOT NULL,
    "CarrierKey" character varying(32) NOT NULL,
    "OtherCarrierName" character varying(120) NOT NULL,
    "Name" character varying(180) NOT NULL,
    "AccountId" uuid,
    "ClientId" uuid,
    "AccountNumber" character varying(120) NOT NULL,
    "PlanName" character varying(160) NOT NULL,
    "PlanType" character varying(32) NOT NULL,
    "DeliveryType" character varying(32) NOT NULL,
    "DeviceModel" character varying(120) NOT NULL,
    "SerialNumber" character varying(120) NOT NULL,
    "KitNumber" character varying(120) NOT NULL,
    "SpeedMbps" numeric(12,2),
    "MonthlyCost" numeric(14,2),
    "PhysicalLocation" character varying(500) NOT NULL,
    "Lan" character varying(80) NOT NULL,
    "Ddns" character varying(253) NOT NULL,
    "SupportPhone" character varying(60) NOT NULL,
    "FixedIp" character varying(64) NOT NULL,
    "SubnetMask" character varying(64) NOT NULL,
    "Gateway" character varying(64) NOT NULL,
    "Dns" character varying(200) NOT NULL,
    "ContractMonths" integer,
    "ContractStartsOn" date,
    "ContractEndsOn" date,
    "Notes" character varying(3000) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_ServiceInventoryItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ServiceInventoryItems_ServiceInventoryAccounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "ServiceInventoryAccounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ServiceInventoryItems_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_ServiceInventoryItems_AccountId" ON "ServiceInventoryItems" ("AccountId");
CREATE INDEX "IX_ServiceInventoryItems_ClientId" ON "ServiceInventoryItems" ("ClientId");
CREATE INDEX "IX_ServiceInventoryItems_CarrierKey_IsActive" ON "ServiceInventoryItems" ("CarrierKey", "IsActive");

END IF;
END $inventory$;


-- Evita marcar como aplicada una migración con tablas incompletas o incompatibles.
DO $verify$
DECLARE expected record; actual_type text; actual_not_null boolean;
BEGIN
    FOR expected IN SELECT * FROM (VALUES
        ('ServiceInventoryAccounts', 'Id', 'uuid', true),
        ('ServiceInventoryAccounts', 'CarrierKey', 'character varying(32)', true),
        ('ServiceInventoryAccounts', 'OtherCarrierName', 'character varying(120)', true),
        ('ServiceInventoryAccounts', 'Label', 'character varying(140)', true),
        ('ServiceInventoryAccounts', 'Email', 'character varying(256)', true),
        ('ServiceInventoryAccounts', 'AccountNumber', 'character varying(120)', true),
        ('ServiceInventoryAccounts', 'PasswordProtected', 'text', true),
        ('ServiceInventoryAccounts', 'Notes', 'character varying(2000)', true),
        ('ServiceInventoryAccounts', 'IsActive', 'boolean', true),
        ('ServiceInventoryAccounts', 'CreatedAt', 'timestamp with time zone', true),
        ('ServiceInventoryAccounts', 'UpdatedAt', 'timestamp with time zone', true),
        ('ServiceInventoryAccounts', 'Version', 'uuid', true),
        ('ServiceInventoryItems', 'Id', 'uuid', true),
        ('ServiceInventoryItems', 'CarrierKey', 'character varying(32)', true),
        ('ServiceInventoryItems', 'OtherCarrierName', 'character varying(120)', true),
        ('ServiceInventoryItems', 'Name', 'character varying(180)', true),
        ('ServiceInventoryItems', 'AccountId', 'uuid', false),
        ('ServiceInventoryItems', 'ClientId', 'uuid', false),
        ('ServiceInventoryItems', 'AccountNumber', 'character varying(120)', true),
        ('ServiceInventoryItems', 'PlanName', 'character varying(160)', true),
        ('ServiceInventoryItems', 'PlanType', 'character varying(32)', true),
        ('ServiceInventoryItems', 'DeliveryType', 'character varying(32)', true),
        ('ServiceInventoryItems', 'DeviceModel', 'character varying(120)', true),
        ('ServiceInventoryItems', 'SerialNumber', 'character varying(120)', true),
        ('ServiceInventoryItems', 'KitNumber', 'character varying(120)', true),
        ('ServiceInventoryItems', 'SpeedMbps', 'numeric(12,2)', false),
        ('ServiceInventoryItems', 'MonthlyCost', 'numeric(14,2)', false),
        ('ServiceInventoryItems', 'PhysicalLocation', 'character varying(500)', true),
        ('ServiceInventoryItems', 'Lan', 'character varying(80)', true),
        ('ServiceInventoryItems', 'Ddns', 'character varying(253)', true),
        ('ServiceInventoryItems', 'SupportPhone', 'character varying(60)', true),
        ('ServiceInventoryItems', 'FixedIp', 'character varying(64)', true),
        ('ServiceInventoryItems', 'SubnetMask', 'character varying(64)', true),
        ('ServiceInventoryItems', 'Gateway', 'character varying(64)', true),
        ('ServiceInventoryItems', 'Dns', 'character varying(200)', true),
        ('ServiceInventoryItems', 'ContractMonths', 'integer', false),
        ('ServiceInventoryItems', 'ContractStartsOn', 'date', false),
        ('ServiceInventoryItems', 'ContractEndsOn', 'date', false),
        ('ServiceInventoryItems', 'Notes', 'character varying(3000)', true),
        ('ServiceInventoryItems', 'IsActive', 'boolean', true),
        ('ServiceInventoryItems', 'CreatedAt', 'timestamp with time zone', true),
        ('ServiceInventoryItems', 'UpdatedAt', 'timestamp with time zone', true),
        ('ServiceInventoryItems', 'Version', 'uuid', true)
    ) AS columns_to_check(table_name, column_name, data_type, is_required)
    LOOP
        SELECT format_type(a.atttypid, a.atttypmod), a.attnotnull INTO actual_type, actual_not_null
        FROM pg_attribute a
        WHERE a.attrelid = to_regclass(format('public.%I', expected.table_name))
          AND a.attname = expected.column_name AND NOT a.attisdropped;
        IF actual_type IS DISTINCT FROM expected.data_type OR actual_not_null IS DISTINCT FROM expected.is_required THEN
            RAISE EXCEPTION 'Columna incompatible %.%. Se cancela la migración.', expected.table_name, expected.column_name;
        END IF;
    END LOOP;
END $verify$;

-- Otorga DML solamente al rol configurado, nunca a PUBLIC ni sobre tablas previas.
DO $permissions$
DECLARE app_role text := nullif(current_setting('hncontrol.application_role', true), '');
BEGIN
    IF app_role IS NOT NULL THEN
        IF lower(app_role) = 'public' OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = app_role) THEN
            RAISE EXCEPTION 'El rol de HN Control debe ser un rol existente y no puede ser PUBLIC.';
        END IF;
        IF NOT has_table_privilege(app_role, 'public."Clients"', 'SELECT') THEN
            RAISE EXCEPTION 'El rol seleccionado no tiene acceso al esquema actual de HN Control.';
        END IF;
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public."ServiceInventoryAccounts", public."ServiceInventoryItems" TO %I', app_role);
    ELSE
        RAISE NOTICE 'Si el usuario web es distinto al propietario que ejecutó este archivo, indique hncontrol.application_role y ejecute el archivo nuevamente para habilitar acceso a las tablas nuevas.';
    END IF;
END $permissions$;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108000100_AddServiceInventory', '8.0.24') ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;

SELECT "MigrationId", "ProductVersion" FROM public."__EFMigrationsHistory"
WHERE "MigrationId" = '20261108000100_AddServiceInventory';
