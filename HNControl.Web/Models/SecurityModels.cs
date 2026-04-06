using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class PermissionRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(80)]
    public string Name { get; set; } = "";

    [MaxLength(400)]
    public string Description { get; set; } = "";

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PermissionRoleModule> Modules { get; set; } = new();
}

public class PermissionRoleModule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PermissionRoleId { get; set; }
    public PermissionRole? PermissionRole { get; set; }

    [Required, MaxLength(60)]
    public string ModuleKey { get; set; } = "";
}

/// <summary>
/// Un usuario (Employee) queda ligado a 1 PermissionRole (rol de módulos).
/// PK = UserId para mantenerlo simple.
/// </summary>
public class UserPermissionRole
{
    [Key, MaxLength(64)]
    public string UserId { get; set; } = "";

    public Guid PermissionRoleId { get; set; }
    public PermissionRole? PermissionRole { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? AssignedByUserId { get; set; }
}

public class PermissionAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(80)]
    public string EventType { get; set; } = "";

    public Guid? PermissionRoleId { get; set; }
    public PermissionRole? PermissionRole { get; set; }

    [MaxLength(80)]
    public string RoleName { get; set; } = "";

    [MaxLength(64)]
    public string? ActorUserId { get; set; }

    [MaxLength(180)]
    public string ActorName { get; set; } = "";

    [MaxLength(1600)]
    public string Details { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LoginTwoFactorChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(64)]
    public string UserId { get; set; } = "";

    [Required, MaxLength(180)]
    public string UserEmail { get; set; } = "";

    [Required, MaxLength(64)]
    public string IpAddress { get; set; } = "";

    [Required, MaxLength(120)]
    public string CodeHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedAttempts { get; set; } = 0;
}
