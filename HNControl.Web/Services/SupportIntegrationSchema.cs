namespace HNControl.Web.Services;
public static class SupportIntegrationSchema
{
    public const string Sql = """
CREATE TABLE IF NOT EXISTS "SupportOrderLinks" (
    "Id" uuid PRIMARY KEY, "SourceId" varchar(80) NOT NULL, "TicketId" bigint NOT NULL,
    "OrderId" uuid NOT NULL REFERENCES "ServiceOrders"("Id") ON DELETE RESTRICT,
    "InventoryServiceId" uuid NULL, "RequestedBy" varchar(200) NOT NULL DEFAULT '',
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportOrderLinks_SourceId_TicketId" ON "SupportOrderLinks"("SourceId","TicketId");
CREATE INDEX IF NOT EXISTS "IX_SupportOrderLinks_OrderId" ON "SupportOrderLinks"("OrderId");
""";
}
