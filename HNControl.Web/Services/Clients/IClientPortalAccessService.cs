using HNControl.Web.Models;

namespace HNControl.Web.Services.Clients;

public interface IClientPortalAccessService
{
    Task<ClientPortalCredentialResult?> EnsureForClientAsync(Guid clientId, string? updatedByUserId = null, bool forceResetPassword = false, CancellationToken ct = default);
    Task<ClientPortalCredentialResult?> GetForClientAsync(Guid clientId, CancellationToken ct = default);
    Task<ClientPortalValidateResult> ValidateAsync(string username, string password, CancellationToken ct = default);
    Task MarkLoginAsync(Guid accessId, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(Guid accessId, string currentPassword, string newPassword, string? updatedByUserId = null, CancellationToken ct = default);
}

public sealed class ClientPortalCredentialResult
{
    public Guid AccessId { get; set; }
    public Guid ClientId { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class ClientPortalValidateResult
{
    public bool IsValid { get; set; }
    public ClientPortalAccess? Access { get; set; }
    public Client? Client { get; set; }
}
