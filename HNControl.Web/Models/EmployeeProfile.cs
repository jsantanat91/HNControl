using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public class EmployeeProfile
{
    [Key]
    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    [MaxLength(200)]
    public string? FullName { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? Nss { get; set; }

    // Guardamos "Gender" (pero varias pantallas usan "Sex")
    [MaxLength(20)]
    public string? Gender { get; set; }

    // ✅ Alias para pantallas/handlers viejos que usan Sex
    [NotMapped]
    public string Sex
    {
        get => Gender ?? "";
        set => Gender = value;
    }

    [MaxLength(120)]
    public string? Position { get; set; }

    [MaxLength(120)]
    public string? EducationLevel { get; set; }

    // --- Datos adicionales (RH) ---

    // Fecha de ingreso (para calcular antigüedad)
    public DateTime? HireDate { get; set; }

    // Fecha de nacimiento
    public DateTime? BirthDate { get; set; }

    // CURP (MX)
    [MaxLength(18)]
    public string? Curp { get; set; }

    // RFC (MX, para nomina timbrada futura)
    [MaxLength(13)]
    public string? Rfc { get; set; }

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [MaxLength(30)]
    public string? EmployeeNumber { get; set; }

    [MaxLength(3)]
    public string? SatContractTypeCode { get; set; }

    [MaxLength(3)]
    public string? SatWorkdayTypeCode { get; set; }

    [MaxLength(3)]
    public string? SatJobRiskCode { get; set; }

    [MaxLength(120)]
    public string? BankName { get; set; }

    [MaxLength(30)]
    public string? BankAccount { get; set; }

    [MaxLength(18)]
    public string? BankClabe { get; set; }

    [MaxLength(500)]
    public string? ProfilePhotoStoragePath { get; set; }

    [MaxLength(120)]
    public string? ProfilePhotoContentType { get; set; }

    [MaxLength(255)]
    public string? ProfilePhotoOriginalFileName { get; set; }

    // Dirección
    [MaxLength(400)]
    public string? Address { get; set; }

    // Sueldo base (lo usamos para cálculo 80/20)
    public decimal SalaryBase { get; set; } = 0m;

    // Vacaciones (días por año). Se calcula "usado" desde LeaveRequests aprobadas.
    public int VacationAllowanceDays { get; set; } = 12;

    // Extras útiles (no afectan lo actual)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
