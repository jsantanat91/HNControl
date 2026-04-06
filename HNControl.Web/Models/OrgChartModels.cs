using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class EmployeeOrgChartNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(64)]
    public string UserId { get; set; } = "";

    [MaxLength(64)]
    public string? ReportsToUserId { get; set; }

    public int SortOrder { get; set; } = 0;
    public int PositionX { get; set; } = 0;
    public int PositionY { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? UpdatedByUserId { get; set; }
}
