using System.ComponentModel.DataAnnotations;

namespace HNControl.Web.Models;

public enum KnowledgeDocType
{
    [Display(Name = "Manual Interno")]
    ManualInterno = 0,
    [Display(Name = "Acceso Plataforma")]
    AccesoPlataforma = 1,
    [Display(Name = "Proceso")]
    Proceso = 2,
    [Display(Name = "Politica")]
    Politica = 3,
    [Display(Name = "Plantilla")]
    Plantilla = 4,
    [Display(Name = "Referencia")]
    Referencia = 5
}

public enum KnowledgeStatus
{
    [Display(Name = "Borrador")]
    Borrador = 0,
    [Display(Name = "Publicado")]
    Publicado = 1,
    [Display(Name = "Archivado")]
    Archivado = 2
}

public static class KnowledgeCatalog
{
    public static readonly string[] Categories =
    {
        "Accesos Plataformas",
        "Manuales Internos"
    };
}

public class KnowledgeLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";

    public KnowledgeDocType DocType { get; set; } = KnowledgeDocType.ManualInterno;
    public KnowledgeStatus Status { get; set; } = KnowledgeStatus.Publicado;

    public string Tags { get; set; } = "";
    public string Body { get; set; } = "";

    public string OwnerUserId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string ReviewerName { get; set; } = "";

    public DateTime? ReviewDueAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? LastViewedAt { get; set; }

    public int ViewCount { get; set; } = 0;
    public bool IsPinned { get; set; } = false;
    public int Version { get; set; } = 1;

    // Adjuntos
    public string AttachmentStoragePath { get; set; } = "";
    public string AttachmentOriginalFileName { get; set; } = "";
    public string AttachmentContentType { get; set; } = "";
    public long? AttachmentSizeBytes { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? ClientServiceContractId { get; set; }
    public ClientServiceContract? ClientServiceContract { get; set; }

    // Acceso interno (secreto protegido)
    public string AccessUsername { get; set; } = "";
    public string AccessSecretProtected { get; set; } = "";
    public string AccessNotes { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
