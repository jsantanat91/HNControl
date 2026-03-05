using System.Text.Json.Serialization;

namespace HNControl.Mobile.Models;

public sealed class LoginRequestDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginResponseDto
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public sealed class ApiMessageDto
{
    public string Message { get; set; } = "";
}

public sealed class ServiceOrderListItemDto
{
    public Guid Id { get; set; }
    public string Client { get; set; } = "";
    public string Title { get; set; } = "";
    public int Type { get; set; }
    public int Status { get; set; }
    public int CurrentArea { get; set; }
    public string ClaimedBy { get; set; } = "";
    public bool IsMine { get; set; }
    public bool CanTake { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EstimatedEndDate { get; set; }

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        1 => "Correctivo",
        2 => "Preventivo",
        3 => "Nueva instalacion",
        4 => "Levantamiento tecnico",
        99 => "Global",
        _ => "Tipo " + Type
    };

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        1 => "Creada",
        2 => "En proceso",
        3 => "En revision",
        4 => "Finalizada",
        5 => "Pendiente firma cliente",
        6 => "Rechazada",
        _ => "Estatus " + Status
    };

    [JsonIgnore]
    public string AreaLabel => CurrentArea switch
    {
        1 => "Levantamiento",
        2 => "Materiales",
        3 => "Ejecucion",
        4 => "Cierre tecnico",
        _ => "Area " + CurrentArea
    };

    [JsonIgnore]
    public string TypeBg => Type switch
    {
        1 => "#FFECD8",
        2 => "#DDF8E8",
        3 => "#D9E8FF",
        4 => "#D7F2FF",
        99 => "#EBECF0",
        _ => "#EEF2F7"
    };

    [JsonIgnore]
    public string StatusBg => Status switch
    {
        1 => "#EEF2FF",
        2 => "#D9E8FF",
        3 => "#FFF0D5",
        4 => "#DDF8E8",
        5 => "#FFF0D5",
        6 => "#FDE7E7",
        _ => "#EEF2F7"
    };

    [JsonIgnore]
    public string AreaBg => CurrentArea switch
    {
        1 => "#D7F2FF",
        2 => "#E8F5D7",
        3 => "#E6EEFF",
        4 => "#E9E3FF",
        _ => "#EEF2F7"
    };
}

public sealed class ServiceOrderDetailDto
{
    public Guid Id { get; set; }
    public string Client { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Type { get; set; }
    public int Status { get; set; }
    public int CurrentArea { get; set; }
    public string ClaimedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EstimatedEndDate { get; set; }
    public string LevantamientoNotes { get; set; } = "";
    public string MaterialesNotes { get; set; } = "";
}

public sealed class EmployeeDashboardDto
{
    public EmployeeProfileDto Profile { get; set; } = new();
    public PayrollDto Payroll { get; set; } = new();
    public List<PayrollHistoryPointDto> PayrollHistory { get; set; } = new();
    public List<DeductionDto> Deductions { get; set; } = new();
    public VacationsDto Vacations { get; set; } = new();
    public ExamsDto Exams { get; set; } = new();
    public ViaticWeekDto? CurrentViaticWeek { get; set; }
    public List<InventoryOrderDto> InventoryOrders { get; set; } = new();
}

public sealed class EmployeeProfileDto
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Position { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Nss { get; set; } = "";
    public string Curp { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal SalaryBase { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? BirthDate { get; set; }
    public string SeniorityText { get; set; } = "";
}

public sealed class PayrollDto
{
    public string Period { get; set; } = "";
    public decimal VariablePercent { get; set; }
    public decimal TotalQuincenal { get; set; }
    public decimal DeductionsQuincenal { get; set; }
    public decimal BonusesQuincenal { get; set; }
    public decimal NetQuincenal { get; set; }
}

public sealed class PayrollHistoryPointDto
{
    public string Label { get; set; } = "";
    public decimal VariablePercent { get; set; }
    public decimal NetQuincenal { get; set; }
}

public sealed class DeductionDto
{
    public string Concept { get; set; } = "";
    public string Direction { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal PeriodAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? RemainingAmount { get; set; }
    public int? ProgressPaidPeriods { get; set; }
    public int? ProgressTotalPeriods { get; set; }

    [JsonIgnore]
    public bool IsBonus => string.Equals(Direction, "Bono", StringComparison.OrdinalIgnoreCase);
}

public sealed class VacationsDto
{
    public int Year { get; set; }
    public int AllowanceDays { get; set; }
    public int UsedDays { get; set; }
    public int RemainingDays { get; set; }
    public int PendingRequests { get; set; }
    public DateTime? NextStart { get; set; }
    public DateTime? NextEnd { get; set; }
}

public sealed class ExamsDto
{
    public int Assigned { get; set; }
    public int InProgress { get; set; }
    public int Submitted { get; set; }
    public int Graded { get; set; }
}

public sealed class ViaticWeekDto
{
    public Guid Id { get; set; }
    public DateTime WeekStart { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Billable { get; set; }
}

public sealed class InventoryOrderDto
{
    public Guid AnchorId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string Type { get; set; } = "";
    public string ProjectTitle { get; set; } = "";
    public string ResponsibleName { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public int LinesCount { get; set; }
    public string ItemsPreview { get; set; } = "";
}
