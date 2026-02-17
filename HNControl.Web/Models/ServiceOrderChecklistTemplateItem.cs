using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class ServiceOrderChecklistTemplateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TemplateId { get; set; }
    public ServiceOrderChecklistTemplate? Template { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(80)]
    public string Category { get; set; } = "General";

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public bool IsRequired { get; set; } = true;
}
