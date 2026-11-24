-- HN Control Web 2.6.3 · Dictamen de viabilidad en Factibilidades.
-- Opcional: la web agrega estas columnas al iniciar.
-- Ejecutar a mano solo si el usuario de la aplicación no tiene permiso ALTER sobre "LevantamientoSurveys".
-- Aditivo: no modifica datos existentes.

ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "ReviewChecksJson" text NOT NULL DEFAULT '[]';
ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "DecisionComments" varchar(4000) NOT NULL DEFAULT '';
ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "DecidedAt" timestamptz NULL;
ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "DecidedByUserId" varchar(64) NULL;
ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "DecisionEmailTo" varchar(256) NOT NULL DEFAULT '';
ALTER TABLE "LevantamientoSurveys" ADD COLUMN IF NOT EXISTS "DecisionEmailSentAt" timestamptz NULL;
