-- Aplicar como propietario de las tablas ANTES de desplegar web 2.5.20.
-- No genera comisiones retroactivas ni modifica salarios o bonos existentes.
BEGIN;
ALTER TABLE "EmployeeProfiles" ADD COLUMN IF NOT EXISTS "ServiceOrderCommissionEnabled" boolean NOT NULL DEFAULT false;
ALTER TABLE "EmployeeProfiles" ADD COLUMN IF NOT EXISTS "ServiceOrderCommissionAmount" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE "EmployeeDeductions" ADD COLUMN IF NOT EXISTS "SourceServiceOrderId" uuid NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmployeeDeductions_SourceServiceOrderId" ON "EmployeeDeductions" ("SourceServiceOrderId");
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20261109232629_AddServiceOrderCommissions', '8.0.24') ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;
SELECT column_name FROM information_schema.columns WHERE table_name = 'EmployeeProfiles' AND column_name LIKE 'ServiceOrderCommission%';
