using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations;

[Migration("202602232200_AddInventoryBrandFields")]
public partial class AddInventoryBrandFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotente: puedes correrlo aunque ya hayas aplicado el script en pgAdmin.
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.""InventoryBrands"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_InventoryBrands"" PRIMARY KEY (""Id"")
);

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_InventoryBrands_Name"" ON public.""InventoryBrands"" (""Name"");

ALTER TABLE public.""InventoryItems""
    ADD COLUMN IF NOT EXISTS ""BrandId"" uuid NULL,
    ADD COLUMN IF NOT EXISTS ""Model"" character varying(120) NULL,
    ADD COLUMN IF NOT EXISTS ""Location"" character varying(200) NULL;

-- SKU opcional (si ya es nullable, no pasa nada)
DO $$
BEGIN
    BEGIN
        ALTER TABLE public.""InventoryItems"" ALTER COLUMN ""Sku"" DROP NOT NULL;
    EXCEPTION WHEN undefined_column THEN
        -- si la columna no existe con ese nombre, lo omitimos (evita tronar el arranque)
        NULL;
    WHEN others THEN
        NULL;
    END;
END $$;

-- Normaliza SKU vacíos a NULL (para que sea realmente opcional)
UPDATE public.""InventoryItems"" SET ""Sku"" = NULL WHERE ""Sku"" = '';

CREATE INDEX IF NOT EXISTS ""IX_InventoryItems_BrandId"" ON public.""InventoryItems"" (""BrandId"");

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_schema='public'
          AND table_name='InventoryItems'
          AND constraint_name='FK_InventoryItems_InventoryBrands_BrandId'
    ) THEN
        ALTER TABLE public.""InventoryItems""
            ADD CONSTRAINT ""FK_InventoryItems_InventoryBrands_BrandId""
            FOREIGN KEY (""BrandId"") REFERENCES public.""InventoryBrands"" (""Id"")
            ON DELETE SET NULL;
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_schema='public'
          AND table_name='InventoryItems'
          AND constraint_name='FK_InventoryItems_InventoryBrands_BrandId'
    ) THEN
        ALTER TABLE public.""InventoryItems"" DROP CONSTRAINT ""FK_InventoryItems_InventoryBrands_BrandId"";
    END IF;
END $$;

ALTER TABLE public.""InventoryItems""
    DROP COLUMN IF EXISTS ""BrandId"",
    DROP COLUMN IF EXISTS ""Model"",
    DROP COLUMN IF EXISTS ""Location"";

DROP INDEX IF EXISTS ""IX_InventoryItems_BrandId"";
DROP INDEX IF EXISTS ""IX_InventoryBrands_Name"";
DROP TABLE IF EXISTS public.""InventoryBrands"";
");
    }
}
