-- Run this script with the OWNER of the SystemConfigurations table.
-- It is safe to execute more than once.

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoAccessTokenProtected" character varying(2200) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoPublicKey" character varying(220) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoWebhookSecretProtected" character varying(2200) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "PublicBaseUrl" character varying(220) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppEnabled" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppGatewayUrl" character varying(300) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppApiKeyProtected" character varying(2200) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppInternalPhonesCsv" character varying(1000) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyTickets" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppNotifyCustomers" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplate" character varying(2000) NOT NULL DEFAULT '';

ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppPayrollReceiptTemplate" character varying(2000) NOT NULL DEFAULT '';
