-- Ejecutar en la base de HN Control, con el propietario de ServiceOrders, ANTES de desplegar web 2.5.18.
-- Conserva órdenes y formatos históricos. Se puede ejecutar más de una vez.
BEGIN;
ALTER TABLE "ServiceOrders" ADD COLUMN IF NOT EXISTS "DeliveryJson" text NOT NULL DEFAULT '';
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261109000200_AddOrderDelivery', '8.0.24')
ON CONFLICT ("MigrationId") DO NOTHING;
COMMIT;
SELECT column_name FROM information_schema.columns WHERE table_name = 'ServiceOrders' AND column_name = 'DeliveryJson';
