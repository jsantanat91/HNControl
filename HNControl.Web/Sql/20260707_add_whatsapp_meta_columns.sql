-- ============================================================================
-- HN Control · Columnas WhatsApp (Meta Cloud API) + Mercado Pago
-- Ejecutar UNA vez con el usuario OWNER de la tabla (o un superusuario, p.ej. postgres).
-- Es idempotente: se puede correr varias veces sin dañar datos.
-- ============================================================================

-- WhatsApp · Meta Cloud API
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppWabaId" character varying(64) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppGraphApiVersion" character varying(12) NOT NULL DEFAULT 'v21.0';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppTemplateLanguage" character varying(12) NOT NULL DEFAULT 'es_MX';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppWebhookVerifyToken" character varying(120) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppOtpTemplateName" character varying(200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppPayrollTemplateName" character varying(200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "WhatsAppTicketTemplateName" character varying(200) NOT NULL DEFAULT '';

-- Mercado Pago (el banner indica que faltan en tu BD)
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoAccessTokenProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoPublicKey" character varying(220) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "MercadoPagoWebhookSecretProtected" character varying(2200) NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS public."SystemConfigurations"
    ADD COLUMN IF NOT EXISTS "PublicBaseUrl" character varying(220) NOT NULL DEFAULT '';

-- ============================================================================
-- RECOMENDADO: hacer a hncontrol_user dueño de la tabla para que HN Control
-- pueda auto-crear columnas en el arranque y no vuelvas a necesitar este script.
-- (Ejecutar como superusuario. Descomenta la linea.)
-- ALTER TABLE public."SystemConfigurations" OWNER TO hncontrol_user;
-- ============================================================================
