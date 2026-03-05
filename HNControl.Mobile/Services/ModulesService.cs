using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class ModulesService
{
    private readonly MobileApiClient _api;

    public ModulesService(MobileApiClient api)
    {
        _api = api;
    }

    public Task<List<ModuleItemDto>> GetAllowedModulesAsync()
        => _api.GetJsonAsync<List<ModuleItemDto>>("api/mobile/modules");

    public Task<List<MonitorItemDto>> GetMonitoringAsync()
        => _api.GetJsonAsync<List<MonitorItemDto>>("api/mobile/modules/monitoring");

    public Task<List<InventoryModuleOrderDto>> GetInventoryRequestsAsync()
        => _api.GetJsonAsync<List<InventoryModuleOrderDto>>("api/mobile/modules/inventory/my-requests");

    public Task<List<CarrierClientDto>> GetCarriersAsync()
        => _api.GetJsonAsync<List<CarrierClientDto>>("api/mobile/modules/carriers");
}
