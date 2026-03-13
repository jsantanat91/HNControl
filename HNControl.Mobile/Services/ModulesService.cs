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
    public Task<MonitorDetailDto> GetMonitoringDetailAsync(Guid id)
        => _api.GetJsonAsync<MonitorDetailDto>($"api/mobile/modules/monitoring/{id}");

    public Task<List<InventoryModuleOrderDto>> GetInventoryRequestsAsync()
        => _api.GetJsonAsync<List<InventoryModuleOrderDto>>("api/mobile/modules/inventory/my-requests");
    public Task<InventoryCatalogDto> GetInventoryCatalogAsync()
        => _api.GetJsonAsync<InventoryCatalogDto>("api/mobile/modules/inventory/catalog");
    public Task<ApiMessageDto> CreateInventoryRequestAsync(InventoryCreateRequestDto body)
        => _api.PostJsonAsync<InventoryCreateRequestDto, ApiMessageDto>("api/mobile/modules/inventory/request", body);

    public Task<List<CarrierClientDto>> GetCarriersAsync()
        => _api.GetJsonAsync<List<CarrierClientDto>>("api/mobile/modules/carriers");
    public Task<CarrierClientDetailDto> GetCarrierClientDetailAsync(Guid clientId)
        => _api.GetJsonAsync<CarrierClientDetailDto>($"api/mobile/modules/carriers/{clientId}");

    public Task<List<ProjectModuleItemDto>> GetProjectsAsync()
        => _api.GetJsonAsync<List<ProjectModuleItemDto>>("api/mobile/modules/projects");

    public Task<List<KnowledgeModuleItemDto>> GetKnowledgeAsync()
        => _api.GetJsonAsync<List<KnowledgeModuleItemDto>>("api/mobile/modules/knowledge");

    public Task<List<LeaveModuleItemDto>> GetLeavesAsync()
        => _api.GetJsonAsync<List<LeaveModuleItemDto>>("api/mobile/modules/leaves");
    public Task<LeaveDetailDto> GetLeaveDetailAsync(Guid id)
        => _api.GetJsonAsync<LeaveDetailDto>($"api/mobile/modules/leaves/{id}");

    public Task<List<ExamModuleItemDto>> GetExamsAsync()
        => _api.GetJsonAsync<List<ExamModuleItemDto>>("api/mobile/modules/exams");
    public Task<ExamTakeDto> GetExamTakeAsync(Guid assignmentId)
        => _api.GetJsonAsync<ExamTakeDto>($"api/mobile/modules/exams/{assignmentId}");
    public Task<ApiMessageDto> SaveExamAsync(Guid assignmentId, ExamTakeSaveDto body)
        => _api.PostJsonAsync<ExamTakeSaveDto, ApiMessageDto>($"api/mobile/modules/exams/{assignmentId}/save", body);
    public Task<ApiMessageDto> SubmitExamAsync(Guid assignmentId, ExamTakeSaveDto body)
        => _api.PostJsonAsync<ExamTakeSaveDto, ApiMessageDto>($"api/mobile/modules/exams/{assignmentId}/submit", body);

    public Task<List<Eval360ModuleItemDto>> GetEval360Async()
        => _api.GetJsonAsync<List<Eval360ModuleItemDto>>("api/mobile/modules/eval360");
    public Task<Eval360TakeDto> GetEval360TakeAsync(Guid assignmentId)
        => _api.GetJsonAsync<Eval360TakeDto>($"api/mobile/modules/eval360/{assignmentId}");
    public Task<ApiMessageDto> SubmitEval360Async(Guid assignmentId, Eval360SubmitDto body)
        => _api.PostJsonAsync<Eval360SubmitDto, ApiMessageDto>($"api/mobile/modules/eval360/{assignmentId}/submit", body);

    public Task<List<TicketModuleItemDto>> GetTicketsAsync(string status = "open")
        => _api.GetJsonAsync<List<TicketModuleItemDto>>($"api/mobile/modules/tickets?status={Uri.EscapeDataString(status)}");
}
