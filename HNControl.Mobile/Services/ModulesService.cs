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

    public Task<List<ProjectModuleItemDto>> GetProjectsAsync()
        => _api.GetJsonAsync<List<ProjectModuleItemDto>>("api/mobile/modules/projects");

    public Task<List<KnowledgeModuleItemDto>> GetKnowledgeAsync()
        => _api.GetJsonAsync<List<KnowledgeModuleItemDto>>("api/mobile/modules/knowledge");

    public Task<List<LeaveModuleItemDto>> GetLeavesAsync()
        => _api.GetJsonAsync<List<LeaveModuleItemDto>>("api/mobile/modules/leaves");

    public Task<List<ExamModuleItemDto>> GetExamsAsync()
        => _api.GetJsonAsync<List<ExamModuleItemDto>>("api/mobile/modules/exams");

    public Task<List<Eval360ModuleItemDto>> GetEval360Async()
        => _api.GetJsonAsync<List<Eval360ModuleItemDto>>("api/mobile/modules/eval360");
}
