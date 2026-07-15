-- ============================================================================
-- HN Control · Ledger de aplicaciones de deducciones/bonos
-- Registra el avance/saldo REAL (una fila por deduccion x corrida de nomina).
-- Idempotente. Ejecutar como OWNER/superusuario si el usuario de la app no
-- tiene permiso para crear tablas.
-- ============================================================================

CREATE TABLE IF NOT EXISTS public."EmployeeDeductionApplications" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "DeductionId" uuid NOT NULL,
    "UserId" character varying(64) NOT NULL DEFAULT '',
    "PeriodStart" timestamp with time zone NOT NULL,
    "PeriodEnd" timestamp with time zone NOT NULL,
    "PayrollDate" timestamp with time zone NOT NULL,
    "Amount" numeric NOT NULL DEFAULT 0,
    "Direction" integer NOT NULL DEFAULT 1,
    "Concept" character varying(200) NOT NULL DEFAULT '',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "IX_EmployeeDeductionApplications_DeductionId"
    ON public."EmployeeDeductionApplications" ("DeductionId");

-- Evita duplicar la aplicacion de un mismo periodo.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_EmployeeDeductionApplications_Ded_Period"
    ON public."EmployeeDeductionApplications" ("DeductionId", "PeriodStart", "PeriodEnd");
