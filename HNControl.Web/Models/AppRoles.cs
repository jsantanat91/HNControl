using System.Security.Claims;

namespace HNControl.Web.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Employee = "Employee";
    public const string Seller = "Seller";
    public const string InventoryManager = "InventoryManager";
    public const string WarehouseLead = "EncargadoAlmacen";

    public static bool IsGlobalAdmin(ClaimsPrincipal? user)
        => user?.IsInRole(Admin) == true || user?.IsInRole(SuperAdmin) == true;
}
