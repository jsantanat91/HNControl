using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class OrdersService
{
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

    public Task<ApiMessageDto> NextAreaAsync(Guid id)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/area/next", new { });
    }

    public Task<ApiMessageDto> PreviousAreaAsync(Guid id)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/area/previous", new { });
    }

    public Task<ApiMessageDto> SubmitAsync(Guid id)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/orders/{id}/submit", new { });
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
