-- Phase 3: limpieza controlada de duplicados (proyectos/accesos)
-- Ejecutar en pgAdmin con respaldo previo.

BEGIN;

-- 1) Quitar accesos duplicados exactos por proyecto (mantiene el primero por CreatedAt/Id)
WITH dup AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "ProjectId",
                         COALESCE(TRIM("Label"), ''),
                         COALESCE(TRIM("HostOrUrl"), ''),
                         COALESCE(TRIM("Username"), ''),
                         COALESCE(TRIM("PasswordProtected"), '')
            ORDER BY "CreatedAt", "Id"
        ) AS rn
    FROM "ProjectAccesses"
)
DELETE FROM "ProjectAccesses" p
USING dup d
WHERE p."Id" = d."Id"
  AND d.rn > 1;

-- 2) Normalizar texto de notas vacias en proyectos
UPDATE "Projects"
SET "AccessNotes" = ''
WHERE "AccessNotes" IS NULL OR BTRIM("AccessNotes") = '';

UPDATE "Projects"
SET "AdditionalComments" = ''
WHERE "AdditionalComments" IS NULL OR BTRIM("AdditionalComments") = '';

-- 3) Detectar contratos ligados a proyecto incorrecto por cliente (solo reporte)
--    Si devuelve filas, revisar manualmente antes de corregir.
-- SELECT c."Id" AS "ContractId", c."ClientId" AS "ContractClient", p."Id" AS "ProjectId", p."ClientId" AS "ProjectClient"
-- FROM "ClientServiceContracts" c
-- JOIN "Projects" p ON p."Id" = c."ProjectId"
-- WHERE c."ProjectId" IS NOT NULL
--   AND c."ClientId" <> p."ClientId";

COMMIT;
