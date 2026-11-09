using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class OrdersService
{
    public Task<OrderDeliveryResponse> DeliveryAsync(Guid id) => _api.GetJsonAsync<OrderDeliveryResponse>($"api/mobile/orders/{id}/delivery");
    public Task<ApiMessageDto> SaveDeliveryAsync(Guid id, HNControl.Shared.OrderDeliveryData data, string expected) =>
        _api.PutJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/delivery", new { Data = data, Expected = expected });
    private readonly MobileApiClient _api;

    public OrdersService(MobileApiClient api)
    {
        _api = api;
    }

    public Task<List<ServiceOrderListItemDto>> ListAsync(int take = 100)
    {
        var clamped = Math.Clamp(take, 1, 300);
        return _api.GetJsonAsync<List<ServiceOrderListItemDto>>($"api/mobile/orders?take={clamped}");
    }

    public Task<ServiceOrderDetailDto> DetailAsync(Guid id)
    {
        return _api.GetJsonAsync<ServiceOrderDetailDto>($"api/mobile/orders/{id}");
    }

    public Task TakeAsync(Guid id)
    {
        return _api.PostAsync($"api/mobile/orders/{id}/take");
    }

    public Task<byte[]> GetPdfAsync(Guid id)
    {
        return _api.GetBytesAsync($"api/mobile/orders/{id}/pdf");
    }

    public Task<ApiMessageDto> UpdateNotesAsync(Guid id, ServiceOrderNotesUpdateDto dto)
    {
        return _api.PutJsonAsync<ServiceOrderNotesUpdateDto, ApiMessageDto>($"api/mobile/orders/{id}/notes", dto);
    }

    public Task<ApiMessageDto> UpdateChecklistAsync(Guid id, ServiceOrderChecklistUpdateDto dto)
    {
        return _api.PutJsonAsync<ServiceOrderChecklistUpdateDto, ApiMessageDto>($"api/mobile/orders/{id}/checklist", dto);
    }

    public Task<ApiMessageDto> NextAreaAsync(Guid id)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/area/next", new { });
    }

    public Task<ApiMessageDto> PreviousAreaAsync(Guid id)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/area/previous", new { });
    }

    public Task<ApiMessageDto> SubmitAsync(Guid id, string? techSigDataUrl = null, string? clientSigDataUrl = null, string? deliveryVersion = null)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/submit", new { techSigDataUrl, clientSigDataUrl, deliveryVersion });
    }

    public async Task<ApiMessageDto> UploadEvidenceAsync(Guid id, Stream fileStream, string fileName, string? fileContentType)
    {
        return await _api.PostMultipartAsync<ApiMessageDto>(
            $"api/mobile/orders/{id}/evidence",
            new Dictionary<string, string>(),
            fileStream,
            fileName,
            "EvidenceFile",
            fileContentType);
    }
}

public sealed class OrderDeliveryResponse
{
    public HNControl.Shared.OrderDeliveryData Data { get; set; } = new();
    public string Expected { get; set; } = "";
    public string Version { get; set; } = "";
    public bool CanEdit { get; set; }
    public string Title { get; set; } = "";
    public string Client { get; set; } = "";
    public string Address { get; set; } = "";
}
