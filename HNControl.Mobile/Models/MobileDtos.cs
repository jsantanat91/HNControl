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
    public bool IsMine { get; set; }
    public bool CanEdit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EstimatedEndDate { get; set; }
    public string LevantamientoNotes { get; set; } = "";
    public string MaterialesNotes { get; set; } = "";
    public List<ServiceOrderChecklistItemDto> Checklist { get; set; } = new();
    public List<ServiceOrderEvidenceDto> Evidences { get; set; } = new();
}

public sealed class ServiceOrderChecklistItemDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ServiceOrderNotesUpdateDto
{
    public string LevantamientoNotes { get; set; } = "";
    public string MaterialesNotes { get; set; } = "";
}

public sealed class ServiceOrderEvidenceDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string UploadedAtLocal { get; set; } = "";
}

public sealed class ServiceOrderChecklistUpdateDto
{
    public List<ServiceOrderChecklistUpdateItemDto> Items { get; set; } = new();
}

public sealed class ServiceOrderChecklistUpdateItemDto
{
    public Guid Id { get; set; }
    public bool IsDone { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class EmployeeDashboardDto
{
    public EmployeeProfileDto Profile { get; set; } = new();
    public PayrollDto Payroll { get; set; } = new();
    public List<PayrollHistoryPointDto> PayrollHistory { get; set; } = new();
    public List<TicketHistoryPointDto> TicketHistory { get; set; } = new();
    public List<DeductionDto> Deductions { get; set; } = new();
    public VacationsDto Vacations { get; set; } = new();
    public ExamsDto Exams { get; set; } = new();
    public ViaticWeekDto? CurrentViaticWeek { get; set; }
    public List<InventoryOrderDto> InventoryOrders { get; set; } = new();
}

public sealed class TicketHistoryPointDto
{
    public string Label { get; set; } = "";
    public int Resolved { get; set; }
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

public sealed class ModuleItemDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class MonitorItemDto
{
    public Guid Id { get; set; }
    public string Client { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProbeType { get; set; } = "";
    public string Address { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? LastCheckedAt { get; set; }
    public int? LastLatencyMs { get; set; }
    public string LastError { get; set; } = "";
}

public sealed class MonitorCheckDto
{
    public DateTime CheckedAt { get; set; }
    public bool Success { get; set; }
    public int? LatencyMs { get; set; }
    public string Error { get; set; } = "";
}

public sealed class MonitorDetailDto
{
    public Guid Id { get; set; }
    public string Client { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProbeType { get; set; } = "";
    public string Address { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? LastCheckedAt { get; set; }
    public int? LastLatencyMs { get; set; }
    public string LastError { get; set; } = "";
    public string ContractLabel { get; set; } = "";
    public string CarrierServiceLabel { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<MonitorCheckDto> LastChecks { get; set; } = new();
}

public sealed class InventoryModuleOrderDto
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

public sealed class InventoryCatalogItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Stock { get; set; }
}

public sealed class InventoryProjectDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
}

public sealed class InventoryCatalogDto
{
    public List<InventoryCatalogItemDto> Items { get; set; } = new();
    public List<InventoryProjectDto> Projects { get; set; } = new();
}

public sealed class InventoryRequestLineDto
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? AssignedClientId { get; set; }
    public string SerialNumber { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class InventoryCreateRequestDto
{
    public string Type { get; set; } = "Out";
    public Guid? ProjectId { get; set; }
    public string Notes { get; set; } = "";
    public List<InventoryRequestLineDto> Lines { get; set; } = new();
}

public sealed class CarrierClientDto
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = "";
    public int ServicesCount { get; set; }
    public string CarriersSummary { get; set; } = "";
}

public sealed class CarrierServiceDto
{
    public Guid Id { get; set; }
    public string Carrier { get; set; } = "";
    public string ServiceLabel { get; set; } = "";
    public string Plan { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string ContractNumber { get; set; } = "";
    public string CircuitId { get; set; } = "";
    public string ServiceAddress { get; set; } = "";
    public string IpInfo { get; set; } = "";
    public string SupportPhone { get; set; } = "";
    public string Notes { get; set; } = "";
    public string LastNotesSummary { get; set; } = "";
}

public sealed class CarrierClientDetailDto
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string Rfc { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<CarrierServiceDto> Services { get; set; } = new();
}

public sealed class ProjectModuleItemDto
{
    public Guid Id { get; set; }
    public string Client { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EstimatedEndDate { get; set; }
}

public sealed class KnowledgeModuleItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string DocType { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public string Url { get; set; } = "";
}

public sealed class LeaveModuleItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public DateTime RequestedAt { get; set; }
}

public sealed class LeaveDetailDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Reason { get; set; } = "";
    public string AdminComment { get; set; } = "";
    public List<string> EvidenceFiles { get; set; } = new();
}

public sealed class ExamModuleItemDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime AssignedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
}

public sealed class ExamTakeChoiceDto
{
    public Guid ChoiceId { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
}

public sealed class ExamTakeQuestionDto
{
    public Guid QuestionId { get; set; }
    public int Ordinal { get; set; }
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public decimal Points { get; set; }
    public bool IsRequired { get; set; }
    public string TextAnswer { get; set; } = "";
    public List<Guid> SelectedChoiceIds { get; set; } = new();
    public List<ExamTakeChoiceDto> Choices { get; set; } = new();
}

public sealed class ExamTakeDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public List<ExamTakeQuestionDto> Questions { get; set; } = new();
}

public sealed class ExamTakeAnswerInputDto
{
    public Guid QuestionId { get; set; }
    public string? TextAnswer { get; set; }
    public List<Guid>? ChoiceIds { get; set; }
}

public sealed class ExamTakeSaveDto
{
    public List<ExamTakeAnswerInputDto> Answers { get; set; } = new();
}

public sealed class Eval360ModuleItemDto
{
    public Guid AssignmentId { get; set; }
    public string Campaign { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public sealed class TicketModuleItemDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = "";
    public string Client { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Source { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime SlaResponseDueAt { get; set; }
    public DateTime SlaResolutionDueAt { get; set; }
    public bool Breach { get; set; }
    public bool IsMine { get; set; }
    public bool CanTake { get; set; }
}

public sealed class TicketEventDto
{
    public DateTime CreatedAt { get; set; }
    public string EventType { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class TicketDetailDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = "";
    public string Client { get; set; } = "";
    public string Contract { get; set; } = "";
    public string Branch { get; set; } = "";
    public string BranchAddress { get; set; } = "";
    public string Carrier { get; set; } = "";
    public string CarrierService { get; set; } = "";
    public string CarrierAccount { get; set; } = "";
    public string CarrierCircuit { get; set; } = "";
    public string CarrierIp { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Source { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime SlaResponseDueAt { get; set; }
    public DateTime SlaResolutionDueAt { get; set; }
    public bool Breach { get; set; }
    public string ResolutionSummary { get; set; } = "";
    public List<TicketEventDto> Events { get; set; } = new();
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
}

public sealed class TicketAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = "";
}

public sealed class ViaticWeekListItemDto
{
    public Guid Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public int Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BillableAmount { get; set; }
    public int EntriesCount { get; set; }

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        1 => "Borrador",
        2 => "Enviado",
        3 => "Aprobado",
        4 => "Rechazado",
        _ => "Estatus " + Status
    };
}

public sealed class ViaticEntryDto
{
    public Guid Id { get; set; }
    public DateTime DayDate { get; set; }
    public int Category { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsBillable { get; set; }
    public bool HasAttachment { get; set; }

    [JsonIgnore]
    public string CategoryLabel => Category switch
    {
        1 => "Transporte",
        2 => "Gasolina",
        3 => "Material",
        4 => "Otros",
        _ => "Categoria " + Category
    };
}

public sealed class ViaticWeekDetailDto
{
    public Guid Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public int Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BillableAmount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<ViaticEntryDto> Entries { get; set; } = new();
}

public sealed class ViaticUpsertEntryDto
{
    public DateTime DayDate { get; set; }
    public int Category { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsBillable { get; set; }
}

public sealed class ViaticEnsureWeekRequestDto
{
    public DateTime AnyDayInWeek { get; set; }
}

public sealed class ViaticEnsureWeekResponseDto
{
    public Guid Id { get; set; }
}
