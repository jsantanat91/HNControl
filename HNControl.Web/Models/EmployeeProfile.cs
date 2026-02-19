using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HNControl.Web.Models;

public class EmployeeProfile
{
    [Key]
    [MaxLength(64)]
    public string UserId { get; set; } = default!;

    [MaxLength(200)]
    public string FullName { get; set; } = "";

    [MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(40)]
    public string Phone { get; set; } = "";

    [MaxLength(20)]
    public string Nss { get; set; } = "";

    // Guardamos "Gender" (pero varias pantallas usan "Sex")
    [MaxLength(20)]
    public string Gender { get; set; } = "";

    // ✅ Alias para pantallas/handlers viejos que usan Sex
    [NotMapped]
    public string Sex
    {
        get => Gender;
        set => Gender = value ?? "";
    }

    [MaxLength(120)]
    public string Position { get; set; } = "";

    // --- Datos adicionales (RH) ---

    // Fecha de ingreso (para calcular antigüedad)
    public DateTime? HireDate { get; set; }

    // Fecha de nacimiento
    public DateTime? BirthDate { get; set; }

    // CURP (MX)
    [MaxLength(18)]
    public string Curp { get; set; } = "";

    // Dirección
    [MaxLength(400)]
    public string Address { get; set; } = "";

    // Sueldo base (lo usamos para cálculo 80/20)
    public decimal SalaryBase { get; set; } = 0m;

    // Extras útiles (no afectan lo actual)
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
