using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public class ServiceOrderChecklistTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ServiceOrderType Type { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ServiceOrderChecklistTemplateItem> Items { get; set; } = new();
}
