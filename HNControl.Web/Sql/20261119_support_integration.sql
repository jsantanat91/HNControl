-- HN Control Web 2.5.31. Ejecutar como propietario si el usuario de la aplicación no puede crear tablas.
-- Aditivo e idempotente: no modifica órdenes, contratos, inventario ni firmas existentes.
CREATE TABLE IF NOT EXISTS "SupportOrderLinks" (
    "Id" uuid PRIMARY KEY, "SourceId" varchar(80) NOT NULL, "TicketId" bigint NOT NULL,
    "OrderId" uuid NOT NULL REFERENCES "ServiceOrders"("Id") ON DELETE RESTRICT,
    "InventoryServiceId" uuid NULL, "RequestedBy" varchar(200) NOT NULL DEFAULT '',
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportOrderLinks_SourceId_TicketId" ON "SupportOrderLinks"("SourceId","TicketId");
CREATE INDEX IF NOT EXISTS "IX_SupportOrderLinks_OrderId" ON "SupportOrderLinks"("OrderId");
