namespace HNControl.Web.Models;

public class EmployeeProfile
{
    // Usamos UserId como PK para 1:1 contra Identity User
    public string UserId { get; set; } = default!;

    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Nss { get; set; } = "";
    public string Gender { get; set; } = "N/A";
    public string Position { get; set; } = "";
    public string Phone { get; set; } = "";

    // Nómina base (fase 2 usaremos esto para 80/20)
    public decimal SalaryBase { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
