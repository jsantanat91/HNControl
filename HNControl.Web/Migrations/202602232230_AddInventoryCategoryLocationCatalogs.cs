using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations;

[Migration("202602232230_AddInventoryCategoryLocationCatalogs")]
public partial class AddInventoryCategoryLocationCatalogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.""InventoryCategories"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(100) NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_InventoryCategories"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_InventoryCategories_Name"" ON public.""InventoryCategories"" (""Name"");

CREATE TABLE IF NOT EXISTS public.""InventoryLocations"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(200) NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_InventoryLocations"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_InventoryLocations_Name"" ON public.""InventoryLocations"" (""Name"");

-- Seed categorías existentes desde InventoryItems (UUID determinístico)
INSERT INTO public.""InventoryCategories"" (""Id"",""Name"",""IsActive"",""CreatedAt"",""UpdatedAt"")
SELECT
  (
    substring(md5(lower(trim(x.""Name""))) from 1 for 8) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 9 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 13 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 17 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 21 for 12)
  )::uuid,
  x.""Name"",
  TRUE,
  now(),
  now()
FROM (
  SELECT DISTINCT trim(""Category"") as ""Name""
  FROM public.""InventoryItems""
  WHERE ""Category"" IS NOT NULL AND trim(""Category"") <> ''
) x
WHERE NOT EXISTS (
  SELECT 1 FROM public.""InventoryCategories"" c
  WHERE lower(c.""Name"") = lower(x.""Name"")
);

-- Seed ubicaciones existentes desde InventoryItems (UUID determinístico)
INSERT INTO public.""InventoryLocations"" (""Id"",""Name"",""IsActive"",""CreatedAt"",""UpdatedAt"")
SELECT
  (
    substring(md5(lower(trim(x.""Name""))) from 1 for 8) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 9 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 13 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 17 for 4) || '-' ||
    substring(md5(lower(trim(x.""Name""))) from 21 for 12)
  )::uuid,
  x.""Name"",
  TRUE,
  now(),
  now()
FROM (
  SELECT DISTINCT trim(""Location"") as ""Name""
  FROM public.""InventoryItems""
  WHERE ""Location"" IS NOT NULL AND trim(""Location"") <> ''
) x
WHERE NOT EXISTS (
  SELECT 1 FROM public.""InventoryLocations"" l
  WHERE lower(l.""Name"") = lower(x.""Name"")
);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_InventoryCategories_Name"";
DROP TABLE IF EXISTS public.""InventoryCategories"";
DROP INDEX IF EXISTS ""IX_InventoryLocations_Name"";
DROP TABLE IF EXISTS public.""InventoryLocations"";
");
    }
}