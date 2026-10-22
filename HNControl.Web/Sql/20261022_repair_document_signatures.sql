-- HN Control: repair deployment blocked by PostgreSQL 42501 (table ownership).
-- Run the ENTIRE script in the HN Control database as postgres or the table owner.
-- This adds only the columns from the two document migrations. It does not grant
-- privileges, transfer ownership, delete records, or overwrite existing recipients.
-- Restart HN Control after COMMIT. Safe to run again after a successful execution.

BEGIN;
SET LOCAL lock_timeout = '10s';

DO $preflight$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL
        OR to_regclass('public."ClientLegalDocuments"') IS NULL
        OR to_regclass('public."ProjectDeliveryFormats"') IS NULL
        OR to_regclass('public."SystemConfigurations"') IS NULL THEN
        RAISE EXCEPTION 'Wrong database or missing base schema. Select the HN Control database before running this repair.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory"
                   WHERE "MigrationId" = '20260508201704_AddSalesProspectNotes') THEN
        RAISE EXCEPTION 'The migrations preceding the document update are missing. Do not mark them as applied; review the database first.';
    END IF;
END $preflight$;

ALTER TABLE public."ClientLegalDocuments"
    ADD COLUMN IF NOT EXISTS "ProviderSignedByName" character varying(200),
    ADD COLUMN IF NOT EXISTS "ProviderSignedByEmail" character varying(256),
    ADD COLUMN IF NOT EXISTS "ProviderSignedByUserId" character varying(64),
    ADD COLUMN IF NOT EXISTS "ProviderSignedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "CompletionEmailSentAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "SigningSnapshotJson" text;

ALTER TABLE public."ProjectDeliveryFormats"
    ADD COLUMN IF NOT EXISTS "ProviderSignedByName" character varying(200),
    ADD COLUMN IF NOT EXISTS "ProviderSignedByEmail" character varying(256),
    ADD COLUMN IF NOT EXISTS "ProviderSignedByUserId" character varying(64),
    ADD COLUMN IF NOT EXISTS "ProviderSignedAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "CompletionEmailSentAt" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "SigningSnapshotJson" text;

ALTER TABLE public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "DocumentNotificationEmails" character varying(4000) NOT NULL
    DEFAULT E'jsantana@hubnet-solutions.net\ndtaboada@hubnet-solutions.net';

-- Verify actual column definitions before recording the migrations as applied.
-- IF NOT EXISTS alone would silently accept an incompatible pre-existing column.
DO $verify$
DECLARE
    expected record;
    actual_type text;
    actual_not_null boolean;
BEGIN
    FOR expected IN
        SELECT * FROM (VALUES
            ('ClientLegalDocuments', 'ProviderSignedByName', 'character varying(200)', false),
            ('ClientLegalDocuments', 'ProviderSignedByEmail', 'character varying(256)', false),
            ('ClientLegalDocuments', 'ProviderSignedByUserId', 'character varying(64)', false),
            ('ClientLegalDocuments', 'ProviderSignedAt', 'timestamp with time zone', false),
            ('ClientLegalDocuments', 'CompletionEmailSentAt', 'timestamp with time zone', false),
            ('ClientLegalDocuments', 'SigningSnapshotJson', 'text', false),
            ('ProjectDeliveryFormats', 'ProviderSignedByName', 'character varying(200)', false),
            ('ProjectDeliveryFormats', 'ProviderSignedByEmail', 'character varying(256)', false),
            ('ProjectDeliveryFormats', 'ProviderSignedByUserId', 'character varying(64)', false),
            ('ProjectDeliveryFormats', 'ProviderSignedAt', 'timestamp with time zone', false),
            ('ProjectDeliveryFormats', 'CompletionEmailSentAt', 'timestamp with time zone', false),
            ('ProjectDeliveryFormats', 'SigningSnapshotJson', 'text', false),
            ('SystemConfigurations', 'DocumentNotificationEmails', 'character varying(4000)', true)
        ) AS columns_to_check(table_name, column_name, type_name, not_null)
    LOOP
        SELECT format_type(a.atttypid, a.atttypmod), a.attnotnull
        INTO actual_type, actual_not_null
        FROM pg_attribute a
        WHERE a.attrelid = to_regclass(format('public.%I', expected.table_name))
          AND a.attname = expected.column_name AND NOT a.attisdropped;
        IF actual_type IS DISTINCT FROM expected.type_name
            OR actual_not_null IS DISTINCT FROM expected.not_null THEN
            RAISE EXCEPTION 'Incompatible column %.%; transaction cancelled.', expected.table_name, expected.column_name;
        END IF;
    END LOOP;
END $verify$;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20261022000100_AddClientDocumentCountersignature', '8.0.24'),
    ('20261022000200_AddDeliverySignaturesAndNotificationRecipients', '8.0.24')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20261022000100_AddClientDocumentCountersignature',
    '20261022000200_AddDeliverySignaturesAndNotificationRecipients')
ORDER BY "MigrationId";
