-- HN Control Web 2.6.0 · Factibilidades conectadas al levantamiento técnico.
-- Opcional: la web crea esta tabla al iniciar si LevantamientoIntegration__ApiKey está configurada.
-- Ejecutar a mano solo si el usuario de la aplicación no tiene permiso CREATE en el esquema public.
-- Aditivo: no modifica ni borra ServiceFeasibilities (factibilidades anteriores).

CREATE TABLE IF NOT EXISTS "LevantamientoSurveys" (
    "Id" uuid PRIMARY KEY,
    "ExternalId" varchar(120) NOT NULL,
    "ReceivedAt" timestamptz NOT NULL DEFAULT now(),
    "ClientId" uuid NULL REFERENCES "Clients"("Id") ON DELETE SET NULL,
    "ClientCodeSent" varchar(20) NOT NULL DEFAULT '',
    "Rfc" varchar(13) NOT NULL DEFAULT '',
    "Company" varchar(200) NOT NULL DEFAULT '',
    "ContactName" varchar(160) NOT NULL DEFAULT '',
    "ContactRole" varchar(120) NOT NULL DEFAULT '',
    "Email" varchar(256) NOT NULL DEFAULT '',
    "Phone" varchar(60) NOT NULL DEFAULT '',
    "City" varchar(200) NOT NULL DEFAULT '',
    "Address" varchar(400) NOT NULL DEFAULT '',
    "BusinessLine" varchar(180) NOT NULL DEFAULT '',
    "Services" varchar(400) NOT NULL DEFAULT '',
    "Urgency" varchar(120) NOT NULL DEFAULT '',
    "Budget" varchar(120) NOT NULL DEFAULT '',
    "Executive" varchar(120) NOT NULL DEFAULT '',
    "Visit" varchar(120) NOT NULL DEFAULT '',
    "SectionsJson" text NOT NULL DEFAULT '[]',
    "PdfFileName" varchar(200) NOT NULL DEFAULT '',
    "PdfBytes" bytea NULL,
    "SitesFileName" varchar(200) NOT NULL DEFAULT '',
    "SitesBytes" bytea NULL,
    "Status" integer NOT NULL DEFAULT 1,
    "InternalNotes" varchar(4000) NOT NULL DEFAULT '',
    "ServiceOrderId" uuid NULL REFERENCES "ServiceOrders"("Id") ON DELETE SET NULL,
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedByUserId" varchar(64) NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_LevantamientoSurveys_ExternalId" ON "LevantamientoSurveys"("ExternalId");
CREATE INDEX IF NOT EXISTS "IX_LevantamientoSurveys_ClientId_ReceivedAt" ON "LevantamientoSurveys"("ClientId","ReceivedAt");
CREATE INDEX IF NOT EXISTS "IX_LevantamientoSurveys_Status_ReceivedAt" ON "LevantamientoSurveys"("Status","ReceivedAt");
CREATE INDEX IF NOT EXISTS "IX_LevantamientoSurveys_Rfc" ON "LevantamientoSurveys"("Rfc");
