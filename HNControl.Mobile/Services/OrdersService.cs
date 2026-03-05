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
}
