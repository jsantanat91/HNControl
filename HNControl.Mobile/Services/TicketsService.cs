using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class TicketsService
{
    private readonly MobileApiClient _api;

    public TicketsService(MobileApiClient api)
    {
        _api = api;
    }

    public Task<List<TicketModuleItemDto>> ListAsync(string status = "open")
        => _api.GetJsonAsync<List<TicketModuleItemDto>>($"api/mobile/modules/tickets?status={Uri.EscapeDataString(status)}");

    public Task<TicketDetailDto> DetailAsync(Guid id)
        => _api.GetJsonAsync<TicketDetailDto>($"api/mobile/modules/tickets/{id}");

    public Task<ApiMessageDto> TakeAsync(Guid id)
        => _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/modules/tickets/{id}/take", new { });

    public Task<ApiMessageDto> StartAsync(Guid id)
        => _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/modules/tickets/{id}/start", new { });

    public Task<ApiMessageDto> ResolveAsync(Guid id, string summary)
        => _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/modules/tickets/{id}/resolve", new { summary });

    public Task<ApiMessageDto> CloseAsync(Guid id)
        => _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/modules/tickets/{id}/close", new { });

    public Task<ApiMessageDto> AddNoteAsync(Guid id, string note)
        => _api.PostJsonAsync<object, ApiMessageDto>($"api/mobile/modules/tickets/{id}/note", new { note });

    public Task<ApiMessageDto> AddEvidenceAsync(Guid id, string note, Stream fileStream, string fileName, string? fileContentType)
        => _api.PostMultipartAsync<ApiMessageDto>(
            $"api/mobile/modules/tickets/{id}/evidence",
            new Dictionary<string, string> { ["note"] = note ?? "" },
            fileStream,
            fileName,
            "file",
            fileContentType);

    public Task<byte[]> DownloadAttachmentAsync(Guid attachmentId)
        => _api.GetBytesAsync($"api/mobile/modules/tickets/attachments/{attachmentId}");
}
