using System.Security.Cryptography;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services.Clients;

public class ClientPortalAccessService : IClientPortalAccessService
{
    private readonly ApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IPasswordHasher<ClientPortalAccess> _hasher;

    public ClientPortalAccessService(
        ApplicationDbContext db,
        ISecretProtector secretProtector,
        IPasswordHasher<ClientPortalAccess> hasher)
    {
        _db = db;
        _secretProtector = secretProtector;
        _hasher = hasher;
    }

    public async Task<ClientPortalCredentialResult?> EnsureForClientAsync(
        Guid clientId,
        string? updatedByUserId = null,
        bool forceResetPassword = false,
        CancellationToken ct = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, ct);
        if (client == null || string.IsNullOrWhiteSpace(client.ClientCode))
            return null;

        var username = client.ClientCode.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;

        var existing = await _db.ClientPortalAccesses.FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (existing == null)
        {
            var password = GeneratePassword();
            var access = new ClientPortalAccess
            {
                ClientId = clientId,
                Username = username,
                IsActive = true,
                UpdatedByUserId = updatedByUserId,
                PasswordProtected = _secretProtector.Protect(password),
                CreatedAt = now,
                UpdatedAt = now
            };
            access.PasswordHash = _hasher.HashPassword(access, password);
            _db.ClientPortalAccesses.Add(access);
            await _db.SaveChangesAsync(ct);
            return ToResult(access, password);
        }

        var changed = false;
        if (!string.Equals(existing.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            existing.Username = username;
            changed = true;
        }

        string plainPassword;
        if (forceResetPassword)
        {
            plainPassword = GeneratePassword();
            existing.PasswordHash = _hasher.HashPassword(existing, plainPassword);
            existing.PasswordProtected = _secretProtector.Protect(plainPassword);
            existing.IsActive = true;
            changed = true;
        }
        else
        {
            plainPassword = _secretProtector.Unprotect(existing.PasswordProtected);
            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                plainPassword = GeneratePassword();
                existing.PasswordHash = _hasher.HashPassword(existing, plainPassword);
                existing.PasswordProtected = _secretProtector.Protect(plainPassword);
                changed = true;
            }
        }

        if (changed)
        {
            existing.UpdatedAt = now;
            existing.UpdatedByUserId = updatedByUserId;
            await _db.SaveChangesAsync(ct);
        }

        return ToResult(existing, plainPassword);
    }

    public async Task<ClientPortalCredentialResult?> GetForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var access = await _db.ClientPortalAccesses.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (access == null) return null;
        var plain = _secretProtector.Unprotect(access.PasswordProtected);
        return ToResult(access, plain);
    }

    public async Task<ClientPortalValidateResult> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        username = (username ?? "").Trim().ToUpperInvariant();
        password = (password ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new ClientPortalValidateResult { IsValid = false };

        var access = await _db.ClientPortalAccesses
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Username == username && x.IsActive, ct);

        if (access == null || access.Client == null || !access.Client.IsActive)
            return new ClientPortalValidateResult { IsValid = false };

        var result = _hasher.VerifyHashedPassword(access, access.PasswordHash ?? "", password);
        var valid = result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        if (!valid) return new ClientPortalValidateResult { IsValid = false };

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            access.PasswordHash = _hasher.HashPassword(access, password);
            access.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new ClientPortalValidateResult
        {
            IsValid = true,
            Access = access,
            Client = access.Client
        };
    }

    public async Task MarkLoginAsync(Guid accessId, CancellationToken ct = default)
    {
        var access = await _db.ClientPortalAccesses.FirstOrDefaultAsync(x => x.Id == accessId, ct);
        if (access == null) return;
        access.LastLoginAt = DateTime.UtcNow;
        access.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private ClientPortalCredentialResult ToResult(ClientPortalAccess access, string password) => new()
    {
        AccessId = access.Id,
        ClientId = access.ClientId,
        Username = access.Username,
        Password = password,
        IsActive = access.IsActive,
        LastLoginAt = access.LastLoginAt
    };

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@$!%*?&";
        var data = new byte[14];
        RandomNumberGenerator.Fill(data);
        var arr = new char[data.Length];
        for (var i = 0; i < data.Length; i++)
            arr[i] = chars[data[i] % chars.Length];
        return new string(arr);
    }
}
