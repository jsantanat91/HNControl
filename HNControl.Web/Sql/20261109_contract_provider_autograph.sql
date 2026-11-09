-- Ejecutar en la base de HN Control como propietario de las tablas antes del deploy 2.5.15
-- cuando la cuenta de la aplicación no tiene permisos para aplicar migraciones.
BEGIN;
ALTER TABLE "ClientLegalDocuments" ADD COLUMN IF NOT EXISTS "ProviderSignatureStoragePath" character varying(500) NULL;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261109000100_AddContractProviderAutograph', '8.0.24')
ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;
