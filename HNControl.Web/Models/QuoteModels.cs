using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum QuoteSegment
{
    Residential = 1,
    Business = 2
}

public enum QuoteNodeType
{
    Category = 1,
    Service = 2,
    Subproduct = 3
}

public enum QuoteRequestStatus
{
    New = 1,
    Emailed = 2,
    EmailError = 3
}

public enum QuoteRuleAction
{
    ShowOnlyIfSelected = 1
}

public class QuoteCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public QuoteSegment Segment { get; set; }
    public QuoteNodeType NodeType { get; set; }
    public Guid? ParentId { get; set; }

    [MaxLength(140)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string? Description { get; set; }

    public decimal? UnitPrice { get; set; }
    public bool UnitPriceIncludesVat { get; set; }
    public bool IsManualPrice { get; set; }

    [MaxLength(600)]
    public string? ReferenceUrl { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QuoteCatalogRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public QuoteSegment Segment { get; set; }
    public Guid TargetItemId { get; set; }
    public Guid RequiredItemId { get; set; }
    public QuoteRuleAction Action { get; set; } = QuoteRuleAction.ShowOnlyIfSelected;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QuoteRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ClientId { get; set; }

    [MaxLength(30)]
    public string Folio { get; set; } = string.Empty;

    public QuoteSegment Segment { get; set; }
    public QuoteRequestStatus Status { get; set; } = QuoteRequestStatus.New;

    [MaxLength(160)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(40)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(260)]
    public string CustomerLocation { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(1200)]
    public string? Notes { get; set; }

    public decimal SubtotalAuto { get; set; }
    public decimal SubtotalBeforeVat { get; set; }
    public decimal VatAmount { get; set; }
    public int ManualItemsCount { get; set; }
    public decimal? EstimatedTotal { get; set; }

    [MaxLength(500)]
    public string? PdfStoragePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }
    public List<QuoteRequestLine> Lines { get; set; } = [];
}

public class QuoteRequestLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteRequestId { get; set; }

    [MaxLength(140)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(140)]
    public string ServiceName { get; set; } = string.Empty;

    [MaxLength(140)]
    public string? SubproductName { get; set; }

    [MaxLength(1200)]
    public string? Description { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public bool PriceIncludesVat { get; set; }
    public decimal VatRate { get; set; } = 0.16m;
    public decimal? BaseAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public bool IsManualPrice { get; set; }
    public decimal? LineTotal { get; set; }

    public QuoteRequest? QuoteRequest { get; set; }
}
