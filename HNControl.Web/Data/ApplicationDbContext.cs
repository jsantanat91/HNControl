using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<ViaticWeek> ViaticWeeks => Set<ViaticWeek>();
    public DbSet<ViaticEntry> ViaticEntries => Set<ViaticEntry>();
    public DbSet<ViaticAttachment> ViaticAttachments => Set<ViaticAttachment>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientService> ClientServices => Set<ClientService>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAccess> ProjectAccesses => Set<ProjectAccess>();

    public DbSet<KnowledgeLink> KnowledgeLinks => Set<KnowledgeLink>();

    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderChecklistTemplate> ServiceOrderChecklistTemplates => Set<ServiceOrderChecklistTemplate>();
    public DbSet<ServiceOrderChecklistTemplateItem> ServiceOrderChecklistTemplateItems => Set<ServiceOrderChecklistTemplateItem>();
    public DbSet<ServiceOrderChecklistItem> ServiceOrderChecklistItems => Set<ServiceOrderChecklistItem>();
    public DbSet<ServiceOrderEvidence> ServiceOrderEvidences => Set<ServiceOrderEvidence>();
    public DbSet<ServiceOrderSignature> ServiceOrderSignatures => Set<ServiceOrderSignature>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<EmployeeProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Nss).HasMaxLength(50);
            e.Property(x => x.Gender).HasMaxLength(20);
            e.Property(x => x.Position).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.SalaryBase).HasColumnType("numeric(12,2)");
        });

        b.Entity<ViaticWeek>(w =>
        {
            w.HasKey(x => x.Id);
            w.Property(x => x.WeekStartDate).HasColumnType("date");
            w.Property(x => x.UpdatedAt).IsConcurrencyToken(false);

            w.HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();

            // Link con perfil (para panel admin)
            w.HasOne(x => x.EmployeeProfile)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .HasPrincipalKey(p => p.UserId);

            w.HasMany(x => x.Entries)
             .WithOne(e => e.Week!)
             .HasForeignKey(e => e.WeekId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ViaticEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DayDate).HasColumnType("date");
            e.Property(x => x.Amount).HasColumnType("numeric(12,2)");
            e.Property(x => x.Description).HasMaxLength(300);

            e.HasOne(x => x.Attachment)
             .WithOne(a => a.Entry!)
             .HasForeignKey<ViaticAttachment>(a => a.EntryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ViaticAttachment>(a =>
        {
            a.HasKey(x => x.Id);
            a.Property(x => x.OriginalFileName).HasMaxLength(255);
            a.Property(x => x.ContentType).HasMaxLength(100);
            a.Property(x => x.StoragePath).HasMaxLength(500);
        });

        b.Entity<Client>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Address).HasMaxLength(400);
            e.HasMany(x => x.Services).WithOne(s => s.Client!).HasForeignKey(s => s.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClientService>(e =>
        {
            e.HasKey(x => new { x.ClientId, x.ServiceType });
        });

        b.Entity<Project>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Objective).HasMaxLength(400);
            e.Property(x => x.Scope).HasMaxLength(800);

            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);

            e.HasOne(x => x.AssignedEmployee)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .HasPrincipalKey(p => p.UserId);

            e.HasMany(x => x.Accesses).WithOne(a => a.Project!).HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProjectAccess>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.HostOrUrl).HasMaxLength(300);
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.PasswordProtected).HasMaxLength(2000);
        });

        b.Entity<KnowledgeLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Url).HasMaxLength(600);
            e.Property(x => x.Description).HasMaxLength(600);
        });

        b.Entity<PerformanceReview>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Employee).WithMany()
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(p => p.UserId);
            e.HasIndex(x => new { x.UserId, x.PeriodStart, x.PeriodEnd }).IsUnique();
            e.Property(x => x.VariablePercent).HasColumnType("numeric(5,4)");
        });

        b.Entity<ServiceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.PublicToken).HasMaxLength(64);
            e.HasIndex(x => x.PublicToken).IsUnique();

            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);

            e.HasOne(x => x.AssignedEmployee).WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .HasPrincipalKey(p => p.UserId);

            e.HasMany(x => x.Checklist).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Evidences).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Signatures).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceOrderChecklistTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasMany(x => x.Items).WithOne(i => i.Template!).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceOrderChecklistTemplateItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
        });

        b.Entity<ServiceOrderChecklistItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(600);
        });

        b.Entity<ServiceOrderEvidence>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StoragePath).HasMaxLength(500);
        });

        b.Entity<ServiceOrderSignature>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SignedByName).HasMaxLength(200);
            e.Property(x => x.StoragePath).HasMaxLength(500);
        });

    }
}
