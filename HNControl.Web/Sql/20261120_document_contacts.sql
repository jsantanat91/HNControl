-- HN Control Web 2.5.32. Ejecutar ANTES de desplegar la web.
-- PostgreSQL: agrega columnas opcionales; conserva registros y destinatarios históricos.
BEGIN;
ALTER TABLE "ClientServiceContracts" ADD COLUMN IF NOT EXISTS "NotificationContactsJson" text NULL;
ALTER TABLE "ClientLegalDocuments" ADD COLUMN IF NOT EXISTS "NotificationContactsJson" text NULL;
ALTER TABLE "ServiceOrders" ADD COLUMN IF NOT EXISTS "NotificationContactsJson" text NULL;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261120000100_AddDocumentContacts', '8.0.24')
ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;

SELECT table_name, column_name FROM information_schema.columns
WHERE table_schema = 'public' AND column_name = 'NotificationContactsJson'
AND table_name IN ('ClientServiceContracts', 'ClientLegalDocuments', 'ServiceOrders');
