using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using HNControl.Web.Data;
using HNControl.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public sealed class ServiceInventoryService(ApplicationDbContext db, IDataProtectionProvider protection)
{
    private static string Clean(string? value) => value?.Trim() ?? "";
    private IDataProtector PasswordProtector(Guid id) => protection.CreateProtector("HNControl.ServiceInventory.AccountPassword.v1", id.ToString("N"));

    private static List<ServiceInventoryIssue> Validate(object input, string carrier, string? other)
    {
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), validation, true);
        var errors = validation.SelectMany(x => (x.MemberNames.Any() ? x.MemberNames : new[] { "" }).Select(f => new ServiceInventoryIssue(f, x.ErrorMessage ?? "Dato no válido."))).ToList();
        if (!ServiceInventoryCatalog.Carriers.Any(x => x.Id == carrier)) errors.Add(new("CarrierKey", "Selecciona un proveedor válido."));
        if (carrier == "otro" && string.IsNullOrWhiteSpace(other)) errors.Add(new("OtherCarrierName", "Escribe el nombre del proveedor."));
        return errors;
    }

    public async Task<ServiceInventorySaveResult> SaveAccountAsync(ServiceInventoryAccountInput input, Guid? id, string? password, ClaimsPrincipal user)
    {
        if (!AppRoles.IsGlobalAdmin(user)) throw new UnauthorizedAccessException();
        input.Label = Clean(input.Label);
        input.Email = Clean(input.Email);
        var errors = Validate(input, input.CarrierKey, input.OtherCarrierName);
        if (!id.HasValue && (string.IsNullOrEmpty(password) || password.Length > 1024)) errors.Add(new("Password", "Escribe una contraseña de entre 1 y 1024 caracteres."));
        if (input.RecordId == Guid.Empty || (id.HasValue && input.RecordId != id)) errors.Add(new("", "La identificación del registro no es válida. Abre nuevamente el formulario."));
        if (errors.Count > 0) return new(null, errors);
        var account = id.HasValue ? await db.ServiceInventoryAccounts.FindAsync(id.Value) : null;
        if (id.HasValue && account == null) return ServiceInventorySaveResult.Error("", "La cuenta ya no está disponible.");
        if (account != null && account.Version != input.Version) return ServiceInventorySaveResult.Error("", "Otra persona modificó la cuenta. Recarga la página antes de guardar.");
        if (account != null && (account.CarrierKey != input.CarrierKey || (input.CarrierKey == "otro" && !string.Equals(account.OtherCarrierName, Clean(input.OtherCarrierName), StringComparison.OrdinalIgnoreCase))) && await db.ServiceInventoryItems.AnyAsync(x => x.AccountId == id))
            return ServiceInventorySaveResult.Error("CarrierKey", "La cuenta tiene servicios asociados. Desvincúlalos antes de cambiar su proveedor.");
        if (account == null)
        {
            if (await db.ServiceInventoryAccounts.AnyAsync(x => x.Id == input.RecordId)) return ServiceInventorySaveResult.Error("", "Esta cuenta ya fue guardada. Regresa al inventario para consultarla.");
            account = new ServiceInventoryAccount { Id = input.RecordId, PasswordProtected = PasswordProtector(input.RecordId).Protect(password!) };
            db.ServiceInventoryAccounts.Add(account);
        }
        // La contraseña no se modifica al editar, aun si el POST envía un campo adicional.
        account.CarrierKey = input.CarrierKey;
        account.OtherCarrierName = input.CarrierKey == "otro" ? Clean(input.OtherCarrierName) : "";
        account.Label = input.Label;
        account.Email = input.Email;
        account.AccountNumber = Clean(input.AccountNumber);
        account.Notes = Clean(input.Notes);
        account.IsActive = input.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        account.Version = Guid.NewGuid();
        return await SaveAsync(account.Id);
    }

    public async Task<ServiceInventorySaveResult> SaveItemAsync(ServiceInventoryItemInput input, Guid? id, ClaimsPrincipal user)
    {
        if (!AppRoles.IsGlobalAdmin(user)) throw new UnauthorizedAccessException();
        input.Name = Clean(input.Name);
        input.PlanType = Clean(input.PlanType);
        input.DeliveryType = input.CarrierKey == "starlink" ? "Satelital" : Clean(input.DeliveryType);
        input.SupportPhone = input.CarrierKey == "starlink" ? "Online" : Clean(input.SupportPhone);
        var errors = Validate(input, input.CarrierKey, input.OtherCarrierName);
        if (input.RecordId == Guid.Empty || (id.HasValue && input.RecordId != id)) errors.Add(new("", "La identificación del registro no es válida. Abre nuevamente el formulario."));
        var item = id.HasValue ? await db.ServiceInventoryItems.FindAsync(id.Value) : null;
        if (id.HasValue && item == null) return ServiceInventorySaveResult.Error("", "El servicio ya no está disponible.");
        if (item != null && item.Version != input.Version) return ServiceInventorySaveResult.Error("", "Otra persona modificó el servicio. Recarga la página antes de guardar.");
        // Se permite conservar una clasificación histórica sin modificaciones en el mismo proveedor.
        // Los valores nuevos y los cambios de proveedor siempre usan los catálogos actuales.
        var sameProvider = item != null && item.CarrierKey == input.CarrierKey
            && (input.CarrierKey != "otro" || string.Equals(item.OtherCarrierName, Clean(input.OtherCarrierName), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(input.PlanType) && !ServiceInventoryDisplay.GetPlanTypes(input.CarrierKey).Contains(input.PlanType)
            && !(sameProvider && item!.PlanType == input.PlanType))
            errors.Add(new("PlanType", "Selecciona un tipo de plan válido para este proveedor."));
        if (!string.IsNullOrEmpty(input.DeliveryType) && !ServiceInventoryDisplay.GetDeliveryTypes(input.CarrierKey).Contains(input.DeliveryType)
            && !(sameProvider && item!.DeliveryType == input.DeliveryType))
            errors.Add(new("DeliveryType", "Selecciona una tecnología de conexión válida para este proveedor."));

        var unchangedLegacyTerm = item != null && (!item.ContractStartsOn.HasValue || !item.ContractMonths.HasValue)
            && item.ContractStartsOn == input.ContractStartsOn && item.ContractMonths == input.ContractMonths;
        input.ContractEndsOn = null; // El fin enviado por un cliente HTTP nunca determina el vencimiento.
        if (input.ContractStartsOn.HasValue && input.ContractMonths.HasValue)
        {
            input.ContractEndsOn = ServiceInventoryDisplay.CalculateContractEnd(input.ContractStartsOn, input.ContractMonths);
            if (!input.ContractEndsOn.HasValue && input.ContractMonths is >= 1 and <= 240)
                errors.Add(new("ContractMonths", "El plazo excede el rango de fechas permitido. Revisa el inicio y los meses."));
        }
        else if (unchangedLegacyTerm)
            input.ContractEndsOn = item!.ContractEndsOn;
        else if (input.ContractStartsOn.HasValue || input.ContractMonths.HasValue)
            errors.Add(new(input.ContractStartsOn.HasValue ? "ContractMonths" : "ContractStartsOn", "Completa la fecha de inicio y el plazo en meses para calcular el vencimiento."));

        if (input.AccountId.HasValue)
        {
            var account = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.Id == input.AccountId).Select(x => new { x.CarrierKey, x.OtherCarrierName, x.IsActive }).SingleOrDefaultAsync();
            if (account == null || account.CarrierKey != input.CarrierKey || (input.CarrierKey == "otro" && !string.Equals(account.OtherCarrierName, Clean(input.OtherCarrierName), StringComparison.OrdinalIgnoreCase)) || (!account.IsActive && item?.AccountId != input.AccountId))
                errors.Add(new("AccountId", "Selecciona una cuenta activa del mismo proveedor."));
        }
        var existingClientId = item?.ClientId;
        if (input.ClientId.HasValue && !await db.Clients.AnyAsync(x => x.Id == input.ClientId && !x.IsTemporaryLead && (x.IsActive || x.Id == existingClientId)))
            errors.Add(new("ClientId", "Selecciona un cliente válido."));
        if (errors.Count > 0) return new(null, errors);
        if (item == null)
        {
            if (await db.ServiceInventoryItems.AnyAsync(x => x.Id == input.RecordId)) return ServiceInventorySaveResult.Error("", "Este servicio ya fue guardado. Regresa al inventario para consultarlo.");
            item = new ServiceInventoryItem { Id = input.RecordId };
            db.ServiceInventoryItems.Add(item);
        }
        item.CarrierKey = input.CarrierKey;
        item.OtherCarrierName = input.CarrierKey == "otro" ? Clean(input.OtherCarrierName) : "";
        item.Name = input.Name;
        item.AccountId = input.AccountId;
        item.ClientId = input.ClientId;
        item.AccountNumber = Clean(input.AccountNumber);
        item.PlanName = Clean(input.PlanName);
        item.PlanType = Clean(input.PlanType);
        item.DeliveryType = Clean(input.DeliveryType);
        item.DeviceModel = Clean(input.DeviceModel);
        item.SerialNumber = Clean(input.SerialNumber);
        item.KitNumber = Clean(input.KitNumber);
        item.SpeedMbps = input.SpeedMbps;
        item.MonthlyCost = input.MonthlyCost;
        item.PhysicalLocation = Clean(input.PhysicalLocation);
        item.SupportPhone = Clean(input.SupportPhone);
        if (input.HasNetworkDetails)
        {
            item.Lan = Clean(input.Lan);
            item.Ddns = Clean(input.Ddns);
            item.FixedIp = Clean(input.FixedIp);
            item.SubnetMask = Clean(input.SubnetMask);
            item.Gateway = Clean(input.Gateway);
            item.Dns = Clean(input.Dns);
        }
        item.ContractMonths = input.ContractMonths;
        item.ContractStartsOn = input.ContractStartsOn;
        item.ContractEndsOn = input.ContractEndsOn;
        item.Notes = Clean(input.Notes);
        item.IsActive = input.IsActive;
        item.UpdatedAt = DateTime.UtcNow;
        item.Version = Guid.NewGuid();
        return await SaveAsync(item.Id);
    }

    private async Task<ServiceInventorySaveResult> SaveAsync(Guid id)
    {
        try { await db.SaveChangesAsync(); return new(id, Array.Empty<ServiceInventoryIssue>()); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ServiceInventorySaveResult.Error("", "El registro cambió mientras lo editabas. Recarga la página antes de guardar.");
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceInventorySaveResult.Error("", "No se pudo guardar. Comprueba que la cuenta y el cliente sigan disponibles antes de reintentar.");
        }
    }

    public async Task<string?> CopyPasswordAsync(Guid id, ClaimsPrincipal user)
    {
        if (!AppRoles.IsGlobalAdmin(user)) throw new UnauthorizedAccessException();
        var encrypted = await db.ServiceInventoryAccounts.AsNoTracking().Where(x => x.Id == id).Select(x => x.PasswordProtected).SingleOrDefaultAsync();
        if (string.IsNullOrEmpty(encrypted)) return null;
        try { return PasswordProtector(id).Unprotect(encrypted); }
        catch (CryptographicException) { return null; }
    }
}
