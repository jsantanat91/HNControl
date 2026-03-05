using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class ViaticosService
{
    private readonly MobileApiClient _api;

    public ViaticosService(MobileApiClient api)
    {
        _api = api;
    }

    public Task<List<ViaticWeekListItemDto>> GetWeeksAsync(int take = 20)
    {
        var clamped = Math.Clamp(take, 1, 100);
        return _api.GetJsonAsync<List<ViaticWeekListItemDto>>($"api/mobile/viaticos/weeks?take={clamped}");
    }

    public Task<ViaticEnsureWeekResponseDto> EnsureWeekAsync(DateTime anyDayInWeek)
    {
        return _api.PostJsonAsync<DateTime, ViaticEnsureWeekResponseDto>("api/mobile/viaticos/weeks", anyDayInWeek);
    }

    public Task<ViaticWeekDetailDto> GetWeekAsync(Guid id)
    {
        return _api.GetJsonAsync<ViaticWeekDetailDto>($"api/mobile/viaticos/week/{id}");
    }

    public Task<ApiMessageDto> AddEntryAsync(Guid weekId, ViaticUpsertEntryDto dto)
    {
        return _api.PostJsonAsync<ViaticUpsertEntryDto, ApiMessageDto>($"api/mobile/viaticos/week/{weekId}/entries", dto);
    }

    public Task<ApiMessageDto> EditEntryAsync(Guid entryId, ViaticUpsertEntryDto dto)
    {
        return _api.PutJsonAsync<ViaticUpsertEntryDto, ApiMessageDto>($"api/mobile/viaticos/entries/{entryId}", dto);
    }

    public Task DeleteEntryAsync(Guid entryId)
    {
        return _api.DeleteAsync($"api/mobile/viaticos/entries/{entryId}");
    }

    public Task<ApiMessageDto> SubmitWeekAsync(Guid weekId)
    {
        return _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/viaticos/week/{weekId}/submit", new { });
    }
}
