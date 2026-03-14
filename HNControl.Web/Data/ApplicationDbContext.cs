using HNControl.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServiceOrderChecklistTemplate> ServiceOrderChecklistTemplates => Set<ServiceOrderChecklistTemplate>();
    public DbSet<ServiceOrderChecklistTemplateItem> ServiceOrderChecklistTemplateItems => Set<ServiceOrderChecklistTemplateItem>();
    public DbSet<ServiceOrderAuditLog> ServiceOrderAuditLogs => Set<ServiceOrderAuditLog>();

    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<ViaticWeek> ViaticWeeks => Set<ViaticWeek>();
    public DbSet<ViaticEntry> ViaticEntries => Set<ViaticEntry>();
    public DbSet<ViaticAttachment> ViaticAttachments => Set<ViaticAttachment>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientServiceContract> ClientServiceContracts => Set<ClientServiceContract>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAccess> ProjectAccesses => Set<ProjectAccess>();

    public DbSet<KnowledgeLink> KnowledgeLinks => Set<KnowledgeLink>();

    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();

    // --------------------
    // Seguridad / Permisos por módulo
    // --------------------
    public DbSet<PermissionRole> PermissionRoles => Set<PermissionRole>();
    public DbSet<PermissionRoleModule> PermissionRoleModules => Set<PermissionRoleModule>();
    public DbSet<UserPermissionRole> UserPermissionRoles => Set<UserPermissionRole>();

    // --------------------
    // Carrier (Internet)
    // --------------------
    public DbSet<InternetCarrier> InternetCarriers => Set<InternetCarrier>();
    public DbSet<ClientCarrierService> ClientCarrierServices => Set<ClientCarrierService>();
    public DbSet<ClientCarrierNote> ClientCarrierNotes => Set<ClientCarrierNote>();

    // --------------------
    // Inventarios
    // --------------------
 
    public DbSet<InventoryBrand> InventoryBrands => Set<InventoryBrand>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    // --------------------
    // Nómina (Deducciones)
    // --------------------
    public DbSet<EmployeeDeduction> EmployeeDeductions => Set<EmployeeDeduction>();
    public DbSet<PayrollReceiptDispatch> PayrollReceiptDispatches => Set<PayrollReceiptDispatch>();

    // --------------------
    // Monitoreo
    // --------------------
    public DbSet<MonitorTarget> MonitorTargets => Set<MonitorTarget>();
    public DbSet<MonitorCheck> MonitorChecks => Set<MonitorCheck>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    public DbSet<Eval360Competency> Eval360Competencies => Set<Eval360Competency>();
    public DbSet<Eval360Question> Eval360Questions => Set<Eval360Question>();
    public DbSet<Eval360Campaign> Eval360Campaigns => Set<Eval360Campaign>();
    public DbSet<Eval360Assignment> Eval360Assignments => Set<Eval360Assignment>();
    public DbSet<Eval360Answer> Eval360Answers => Set<Eval360Answer>();
    public DbSet<Eval360Comment> Eval360Comments => Set<Eval360Comment>();

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderWorkItem> ServiceOrderWorkItems => Set<ServiceOrderWorkItem>();
    public DbSet<ServiceOrderChecklistItem> ServiceOrderChecklistItems => Set<ServiceOrderChecklistItem>();
    public DbSet<ServiceOrderEvidence> ServiceOrderEvidences => Set<ServiceOrderEvidence>();
    public DbSet<ServiceOrderSignature> ServiceOrderSignatures => Set<ServiceOrderSignature>();
    public DbSet<QuoteCatalogItem> QuoteCatalogItems => Set<QuoteCatalogItem>();
    public DbSet<QuoteCatalogRule> QuoteCatalogRules => Set<QuoteCatalogRule>();
    public DbSet<QuoteRequest> QuoteRequests => Set<QuoteRequest>();
    public DbSet<QuoteRequestLine> QuoteRequestLines => Set<QuoteRequestLine>();

    // --------------------
    // Vacaciones e incidencias
    // --------------------
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveEvidence> LeaveEvidences => Set<LeaveEvidence>();

    // --------------------
    // Exámenes
    // --------------------
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamChoice> ExamChoices => Set<ExamChoice>();
    public DbSet<ExamAssignment> ExamAssignments => Set<ExamAssignment>();
    public DbSet<ExamAnswer> ExamAnswers => Set<ExamAnswer>();
    public DbSet<ExamAnswerAttachment> ExamAnswerAttachments => Set<ExamAnswerAttachment>();
    public DbSet<ExamAnswerChoice> ExamAnswerChoices => Set<ExamAnswerChoice>();

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
            e.Property(x => x.Curp).HasMaxLength(18);
            e.Property(x => x.Address).HasMaxLength(400);
            e.Property(x => x.HireDate).HasColumnType("date");
            e.Property(x => x.BirthDate).HasColumnType("date");
            e.Property(x => x.SalaryBase).HasColumnType("numeric(12,2)");
            e.Property(x => x.VacationAllowanceDays).HasDefaultValue(12);
        });

        // --------------------
        // Nómina (Deducciones)
        // --------------------
        b.Entity<EmployeeDeduction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.Concept).HasMaxLength(200);
            e.Property(x => x.Amount).HasColumnType("numeric(12,2)");
            e.Property(x => x.Rate).HasColumnType("numeric(6,5)");
            e.Property(x => x.StartDate).HasColumnType("date");
            e.Property(x => x.EndDate).HasColumnType("date");
            e.Property(x => x.TotalAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.RemainingAmount).HasColumnType("numeric(12,2)");

            e.HasIndex(x => new { x.UserId, x.IsActive, x.StartDate, x.EndDate });

            e.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PayrollReceiptDispatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.RecipientEmail).HasMaxLength(256);
            e.Property(x => x.PeriodStart).HasColumnType("date");
            e.Property(x => x.PeriodEnd).HasColumnType("date");
            e.Property(x => x.PayrollDate).HasColumnType("date");
            e.Property(x => x.LastError).HasMaxLength(1200);

            e.HasIndex(x => new { x.UserId, x.PeriodStart, x.PeriodEnd }).IsUnique();
            e.HasIndex(x => new { x.PayrollDate, x.IsSent });
        });

        b.Entity<ViaticWeek>(w =>
        {
            w.HasKey(x => x.Id);
            w.Property(x => x.WeekStartDate).HasColumnType("date");
            w.Property(x => x.UpdatedAt).IsConcurrencyToken(false);
            w.Property(x => x.TripDestination).HasMaxLength(220);
            w.Property(x => x.TripPurpose).HasMaxLength(1200);
            w.Property(x => x.RequestedAdvanceAmount).HasColumnType("numeric(12,2)");
            w.Property(x => x.ApprovedAdvanceAmount).HasColumnType("numeric(12,2)");
            w.Property(x => x.FlowType).HasConversion<string>().HasMaxLength(30);
            w.Property(x => x.DepositedByUserId).HasMaxLength(64);
            w.Property(x => x.SettlementApprovedByUserId).HasMaxLength(64);

            w.HasIndex(x => new { x.UserId, x.FlowType, x.WeekStartDate });

            // Link con perfil (para panel admin)
            w.HasOne(x => x.EmployeeProfile)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .HasPrincipalKey(p => p.UserId);

            w.HasOne(x => x.RelatedServiceOrder)
             .WithMany()
             .HasForeignKey(x => x.RelatedServiceOrderId)
             .OnDelete(DeleteBehavior.SetNull);

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
            e.Property(x => x.ClientCode).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.ContactName).HasMaxLength(120);
            e.Property(x => x.Address).HasMaxLength(400);
            e.Property(x => x.PublicQuoteToken).HasMaxLength(80);
            e.HasIndex(x => x.ClientCode).IsUnique();
            e.HasIndex(x => x.PublicQuoteToken).IsUnique();
            e.HasMany(x => x.Contracts)
             .WithOne(s => s.Client!)
             .HasForeignKey(s => s.ClientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClientServiceContract>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.Provider).HasMaxLength(120);
            e.Property(x => x.AccountNumber).HasMaxLength(120);
            e.Property(x => x.ContractNumber).HasMaxLength(120);
            e.Property(x => x.PortalUrl).HasMaxLength(300);
            e.Property(x => x.PortalUsername).HasMaxLength(200);
            e.Property(x => x.PortalPasswordProtected).HasMaxLength(2000);

            e.Property(x => x.ContractStartDate).HasColumnType("date");
            e.Property(x => x.ContractEndDate).HasColumnType("date");

            e.Property(x => x.Notes).HasMaxLength(2000);

            e.Property(x => x.SignedContractStoragePath).HasMaxLength(500);
            e.Property(x => x.SignedContractOriginalFileName).HasMaxLength(255);
            e.Property(x => x.SignedContractContentType).HasMaxLength(100);

            e.HasIndex(x => new { x.ClientId, x.ServiceType, x.Label });

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
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
            e.Property(x => x.Tags).HasMaxLength(500);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.OwnerUserId).HasMaxLength(64);
            e.Property(x => x.OwnerName).HasMaxLength(200);
            e.Property(x => x.ReviewerName).HasMaxLength(200);
            e.Property(x => x.AttachmentStoragePath).HasMaxLength(500);
            e.Property(x => x.AttachmentOriginalFileName).HasMaxLength(255);
            e.Property(x => x.AttachmentContentType).HasMaxLength(100);
            e.Property(x => x.AccessUsername).HasMaxLength(160);
            e.Property(x => x.AccessSecretProtected).HasMaxLength(2000);
            e.Property(x => x.AccessNotes).HasMaxLength(1200);
            e.Property(x => x.UpdatedByName).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.PublishedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.LastViewedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.ReviewDueAt).HasColumnType("timestamp with time zone");

            e.HasIndex(x => new { x.Status, x.DocType, x.Category });
            e.HasIndex(x => x.ReviewDueAt);
            e.HasIndex(x => x.IsPinned);
        });

        b.Entity<PerformanceReview>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Employee).WithMany()
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(p => p.UserId);

            e.HasIndex(x => new { x.UserId, x.PeriodStart, x.PeriodEnd }).IsUnique();
            e.Property(x => x.VariablePercent).HasColumnType("numeric(5,4)");
            e.Property(x => x.Notes).HasMaxLength(3600);
        });

        // --------------------
        // Seguridad / Permisos por módulo
        // --------------------
        b.Entity<PermissionRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(400);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.IsDefault);

            e.HasMany(x => x.Modules)
                .WithOne(m => m.PermissionRole!)
                .HasForeignKey(m => m.PermissionRoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PermissionRoleModule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ModuleKey).HasMaxLength(60);
            e.HasIndex(x => new { x.PermissionRoleId, x.ModuleKey }).IsUnique();
        });

        b.Entity<UserPermissionRole>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.AssignedByUserId).HasMaxLength(64);
            e.HasIndex(x => x.PermissionRoleId);

            e.HasOne(x => x.PermissionRole)
                .WithMany()
                .HasForeignKey(x => x.PermissionRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --------------------
        // Carrier (Internet)
        // --------------------
        b.Entity<InternetCarrier>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.SupportPhone).HasMaxLength(40);
            e.Property(x => x.SupportEmail).HasMaxLength(120);
            e.Property(x => x.SupportPortalUrl).HasMaxLength(400);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<ClientCarrierService>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ServiceLabel).HasMaxLength(140);
            e.Property(x => x.Plan).HasMaxLength(140);
            e.Property(x => x.AccountNumber).HasMaxLength(120);
            e.Property(x => x.ContractNumber).HasMaxLength(120);
            e.Property(x => x.BusinessName).HasMaxLength(180);
            e.Property(x => x.SerialNumber).HasMaxLength(120);
            e.Property(x => x.ServiceType).HasMaxLength(40);
            e.Property(x => x.CircuitId).HasMaxLength(120);
            e.Property(x => x.ServiceAddress).HasMaxLength(200);
            e.Property(x => x.IpInfo).HasMaxLength(200);
            e.Property(x => x.Gateway).HasMaxLength(120);
            e.Property(x => x.GatewayLink).HasMaxLength(120);
            e.Property(x => x.Fqdn).HasMaxLength(180);
            e.Property(x => x.SupportPhoneOverride).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(2000);

            e.HasIndex(x => new { x.ClientId, x.CarrierId });
            e.HasIndex(x => x.ClientServiceContractId);

            e.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Carrier)
                .WithMany(c => c.Services)
                .HasForeignKey(x => x.CarrierId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ClientServiceContract)
                .WithMany()
                .HasForeignKey(x => x.ClientServiceContractId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.CarrierNotes)
                .WithOne(n => n.Service!)
                .HasForeignKey(n => n.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClientCarrierNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TicketNumber).HasMaxLength(120);
            e.Property(x => x.Message).HasMaxLength(3000);
            e.Property(x => x.CreatedByUserId).HasMaxLength(64);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
            e.HasIndex(x => new { x.ServiceId, x.CreatedAt });
        });

        // --------------------
        // Inventarios
        // --------------------
        b.Entity<InventoryBrand>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.Name).IsUnique(true);
        });

        b.Entity<InventoryCategory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.Name).IsUnique(true);
        });

        b.Entity<InventoryLocation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            e.HasIndex(x => x.Name).IsUnique(true);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Sku).HasMaxLength(60);
            e.Property(x => x.ModelCode).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(100);

            e.Property(x => x.Unit).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(2000);

            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.Location).HasMaxLength(200);

            e.Property(x => x.QuantityOnHand).HasColumnType("numeric(18,3)");
            e.Property(x => x.ReorderLevel).HasColumnType("numeric(18,3)");

            e.HasIndex(x => x.Sku).IsUnique(false);
            e.HasIndex(x => x.ModelCode)
                .IsUnique(true)
                .HasFilter("\"ModelCode\" IS NOT NULL AND \"ModelCode\" <> ''");
            e.HasIndex(x => x.BrandId);

            e.HasOne(x => x.Brand)
                .WithMany(b => b.Items)
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.Movements)
                .WithOne(m => m.Item!)
                .HasForeignKey(m => m.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InventoryMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasColumnType("numeric(18,3)");
            e.Property(x => x.SerialNumber).HasMaxLength(120);
            e.Property(x => x.Reference).HasMaxLength(120);
            e.Property(x => x.RequestedByUserId).HasMaxLength(64);
            e.Property(x => x.RequestedByName).HasMaxLength(200);
            e.Property(x => x.ResponsibleUserId).HasMaxLength(64);
            e.Property(x => x.ResponsibleName).HasMaxLength(200);
            e.Property(x => x.ApprovedByUserId).HasMaxLength(64);
            e.Property(x => x.ApprovedByName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.AdminNote).HasMaxLength(2000);

            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ItemId);
        });

        // --------------------
        // Monitoreo
        // --------------------
        b.Entity<MonitorTarget>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Fqdn).HasMaxLength(255);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.SubnetMask).HasMaxLength(32);
            e.Property(x => x.Gateway).HasMaxLength(64);
            e.Property(x => x.HttpUrl).HasMaxLength(600);
            e.Property(x => x.LastError).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(2000);

            e.HasIndex(x => new { x.IsActive, x.NextCheckAt });
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.ClientServiceContractId);
            e.HasIndex(x => x.ClientCarrierServiceId);

            e.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.ClientServiceContract)
                .WithMany()
                .HasForeignKey(x => x.ClientServiceContractId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.ClientCarrierService)
                .WithMany()
                .HasForeignKey(x => x.ClientCarrierServiceId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.Checks)
                .WithOne(c => c.Target!)
                .HasForeignKey(c => c.TargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MonitorCheck>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Error).HasMaxLength(500);
            e.HasIndex(x => new { x.TargetId, x.CheckedAt });
        });

        
        // --------------------
        // Eval 360
        // --------------------
        b.Entity<Eval360Competency>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasIndex(x => x.SortOrder);

            e.HasMany(x => x.Questions)
             .WithOne(q => q.Competency!)
             .HasForeignKey(q => q.CompetencyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Eval360Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(600);
            e.HasIndex(x => new { x.CompetencyId, x.SortOrder });
        });

        b.Entity<Eval360Campaign>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(800);
            e.Property(x => x.PeriodStart).HasColumnType("date");
            e.Property(x => x.PeriodEnd).HasColumnType("date");
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        b.Entity<Eval360Assignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EvaluatorUserId).HasMaxLength(64);
            e.Property(x => x.SubjectUserId).HasMaxLength(64);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
            e.Property(x => x.SubmittedAt).HasColumnType("timestamp with time zone");

            e.HasIndex(x => new { x.CampaignId, x.EvaluatorUserId, x.SubjectUserId }).IsUnique();

            e.HasMany(x => x.Answers)
             .WithOne(a => a.Assignment!)
             .HasForeignKey(a => a.AssignmentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Comments)
             .WithOne(c => c.Assignment!)
             .HasForeignKey(c => c.AssignmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Eval360Answer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AssignmentId, x.QuestionId }).IsUnique();
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

        b.Entity<Eval360Comment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AssignmentId, x.CompetencyId }).IsUnique();
            e.Property(x => x.CommentText).HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        });

b.Entity<ServiceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.PublicToken).HasMaxLength(64);
            e.HasIndex(x => x.PublicToken).IsUnique();

            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.ClientServiceContract).WithMany().HasForeignKey(x => x.ClientServiceContractId).OnDelete(DeleteBehavior.SetNull);

            e.Property(x => x.AssignedUserId).HasMaxLength(64);
            e.Property(x => x.ClaimedByUserId).HasMaxLength(64);

            e.HasOne(x => x.AssignedEmployee).WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.ClaimedByEmployee).WithMany()
                .HasForeignKey(x => x.ClaimedByUserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.WorkItems)
                .WithOne(w => w.Order!)
                .HasForeignKey(w => w.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Checklist).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Evidences).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Signatures).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceOrderWorkItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.WorkPerformed).HasMaxLength(2000);
            e.Property(x => x.MaterialsUsed).HasMaxLength(2000);
            e.Property(x => x.TechnicianNotes).HasMaxLength(2000);
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

            e.HasIndex(x => new { x.OrderId, x.WorkItemId });

            e.HasOne(x => x.WorkItem)
                .WithMany()
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
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

        b.Entity<QuoteCatalogItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(140);
            e.Property(x => x.Description).HasMaxLength(1200);
            e.Property(x => x.ImageUrl).HasMaxLength(600);
            e.Property(x => x.VariantGroup).HasMaxLength(60);
            e.Property(x => x.VariantValue).HasMaxLength(80);
            e.Property(x => x.ReferenceUrl).HasMaxLength(600);
            e.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
            e.HasIndex(x => new { x.Segment, x.NodeType, x.ParentId, x.IsActive });
        });

        b.Entity<QuoteCatalogRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Segment, x.TargetItemId, x.RequiredItemId }).IsUnique();
            e.HasIndex(x => new { x.Segment, x.IsActive });
        });

        b.Entity<QuoteRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Folio).HasMaxLength(30);
            e.Property(x => x.CustomerName).HasMaxLength(160);
            e.Property(x => x.CustomerEmail).HasMaxLength(256);
            e.Property(x => x.CustomerPhone).HasMaxLength(40);
            e.Property(x => x.CustomerLocation).HasMaxLength(260);
            e.Property(x => x.CompanyName).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1200);
            e.Property(x => x.PdfStoragePath).HasMaxLength(500);
            e.Property(x => x.AcceptedByUserId).HasMaxLength(64);
            e.Property(x => x.SubtotalAuto).HasColumnType("numeric(12,2)");
            e.Property(x => x.SubtotalBeforeVat).HasColumnType("numeric(12,2)");
            e.Property(x => x.VatAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.EstimatedTotal).HasColumnType("numeric(12,2)");
            e.HasIndex(x => x.Folio).IsUnique();
            e.HasIndex(x => new { x.Segment, x.CreatedAt });
            e.HasIndex(x => x.ClientId);

            e.HasMany(x => x.Lines)
                .WithOne(l => l.QuoteRequest!)
                .HasForeignKey(l => l.QuoteRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<QuoteRequestLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CategoryName).HasMaxLength(140);
            e.Property(x => x.ServiceName).HasMaxLength(140);
            e.Property(x => x.SubproductName).HasMaxLength(140);
            e.Property(x => x.Description).HasMaxLength(1200);
            e.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
            e.Property(x => x.VatRate).HasColumnType("numeric(6,4)");
            e.Property(x => x.BaseAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.VatAmount).HasColumnType("numeric(12,2)");
            e.Property(x => x.LineTotal).HasColumnType("numeric(12,2)");
            e.Property(x => x.ItemImageUrl).HasMaxLength(600);
            e.HasIndex(x => x.QuoteRequestId);
        });

        // --------------------
        // Tickets (ITIL)
        // --------------------
        b.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TicketNumber).HasMaxLength(40);
            e.Property(x => x.Title).HasMaxLength(220);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Subcategory).HasMaxLength(100);
            e.Property(x => x.AssignedToUserId).HasMaxLength(64);
            e.Property(x => x.AssignedToName).HasMaxLength(200);
            e.Property(x => x.CreatedByUserId).HasMaxLength(64);
            e.Property(x => x.CreatedByName).HasMaxLength(200);
            e.Property(x => x.RequesterName).HasMaxLength(180);
            e.Property(x => x.RequesterEmail).HasMaxLength(256);
            e.Property(x => x.RequesterPhone).HasMaxLength(60);
            e.Property(x => x.RequesterLocation).HasMaxLength(300);
            e.Property(x => x.ResolutionSummary).HasMaxLength(1200);

            e.HasIndex(x => x.TicketNumber).IsUnique();
            e.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt });
            e.HasIndex(x => new { x.ClientId, x.CreatedAt });
            e.HasIndex(x => x.MonitorTargetId);

            e.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ClientServiceContract)
                .WithMany()
                .HasForeignKey(x => x.ClientServiceContractId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.MonitorTarget)
                .WithMany()
                .HasForeignKey(x => x.MonitorTargetId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.AssignedToEmployee)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.Events)
                .WithOne(ev => ev.Ticket!)
                .HasForeignKey(ev => ev.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Attachments)
                .WithOne(a => a.Ticket!)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TicketEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(60);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.HasIndex(x => new { x.TicketId, x.CreatedAt });
        });

        b.Entity<TicketAttachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StoragePath).HasMaxLength(500);
            e.Property(x => x.UploadedByUserId).HasMaxLength(64);
            e.Property(x => x.UploadedByName).HasMaxLength(200);
            e.HasIndex(x => new { x.TicketId, x.UploadedAt });
        });

        // --------------------
        // Vacaciones e incidencias
        // --------------------
        b.Entity<LeaveRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.StartDate).HasColumnType("date");
            e.Property(x => x.EndDate).HasColumnType("date");
            e.Property(x => x.Reason).HasMaxLength(1200);
            e.Property(x => x.AdminComment).HasMaxLength(600);

            e.HasIndex(x => new { x.UserId, x.Status, x.StartDate });

            e.HasOne(x => x.EmployeeProfile)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Evidences)
                .WithOne(a => a.LeaveRequest!)
                .HasForeignKey(a => a.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeaveEvidence>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StoragePath).HasMaxLength(500);
            e.HasIndex(x => x.LeaveRequestId);
        });

        // --------------------
        // Exámenes
        // --------------------
        b.Entity<Exam>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.CreatedByUserId).HasMaxLength(64);

            e.HasMany(x => x.Questions)
                .WithOne(q => q.Exam!)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Assignments)
                .WithOne(a => a.Exam!)
                .HasForeignKey(a => a.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamQuestion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(2000);
            e.Property(x => x.Points).HasColumnType("numeric(12,2)");
            e.HasIndex(x => new { x.ExamId, x.Ordinal });

            e.HasMany(x => x.Choices)
                .WithOne(c => c.Question!)
                .HasForeignKey(c => c.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamChoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.HasIndex(x => new { x.QuestionId, x.Ordinal });
        });

        b.Entity<ExamAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.Score).HasColumnType("numeric(12,2)");
            e.Property(x => x.MaxScore).HasColumnType("numeric(12,2)");
            e.HasIndex(x => new { x.ExamId, x.UserId, x.Status });

            e.HasOne(x => x.EmployeeProfile)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .HasPrincipalKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Answers)
                .WithOne(a => a.Assignment!)
                .HasForeignKey(a => a.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamAnswer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.AutoScore).HasColumnType("numeric(12,2)");
            e.Property(x => x.ManualScore).HasColumnType("numeric(12,2)");
            e.HasIndex(x => new { x.AssignmentId, x.QuestionId }).IsUnique();

            e.HasMany(x => x.SelectedChoices)
                .WithOne(sc => sc.Answer!)
                .HasForeignKey(sc => sc.ExamAnswerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamAnswerChoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ExamAnswerId, x.ChoiceId }).IsUnique();
        });

    }
}


