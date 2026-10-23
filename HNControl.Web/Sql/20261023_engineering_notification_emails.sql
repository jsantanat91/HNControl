-- HN Control: destinatarios de Ingeniería para órdenes de servicio.
-- Ejecutar COMPLETO en la base de HN Control, como postgres o propietario de las
-- tablas, antes de desplegar si el usuario de la aplicación no tiene permisos DDL.
-- No cambia propietarios, no concede permisos ni modifica destinatarios existentes.
-- Es seguro volver a ejecutarlo. Reiniciar HN Control después del COMMIT.

BEGIN;
SET LOCAL lock_timeout = '10s';

DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL
        OR to_regclass('public."SystemConfigurations"') IS NULL THEN
        RAISE EXCEPTION 'Base incorrecta o esquema base incompleto. Seleccione la base de HN Control.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
                   WHERE "MigrationId" = '20261022000200_AddDeliverySignaturesAndNotificationRecipients') THEN
        RAISE EXCEPTION 'Falta la migración anterior de documentos. Actualice ese esquema antes de continuar.';
    END IF;
END $preflight$;

ALTER TABLE public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "EngineeringNotificationEmails" character varying(4000) NOT NULL
    DEFAULT E'jsantana@hubnet-solutions.net\ndtaboada@hubnet-solutions.net';

-- No registrar la migración si una columna preexistente tiene otra definición.
DO $verify$
DECLARE
    actual_type text;
    actual_not_null boolean;
    actual_default text;
BEGIN
    SELECT format_type(a.atttypid, a.atttypmod), a.attnotnull, pg_get_expr(d.adbin, d.adrelid)
    INTO actual_type, actual_not_null, actual_default
    FROM pg_attribute a
    LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE a.attrelid = 'public."SystemConfigurations"'::regclass
      AND a.attname = 'EngineeringNotificationEmails' AND NOT a.attisdropped;
    IF actual_type IS DISTINCT FROM 'character varying(4000)'
        OR actual_not_null IS DISTINCT FROM true
        OR actual_default IS NULL THEN
        RAISE EXCEPTION 'La columna EngineeringNotificationEmails tiene una definición incompatible. Transacción cancelada.';
    END IF;
END $verify$;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261023000100_AddEngineeringNotificationEmails', '8.0.24')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
WHERE "MigrationId" = '20261023000100_AddEngineeringNotificationEmails';
