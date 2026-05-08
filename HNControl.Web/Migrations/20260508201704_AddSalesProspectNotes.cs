using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesProspectNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractTermMonths",
                table: "QuoteRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralTerms",
                table: "QuoteRequests",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recurrence",
                table: "QuoteRequestLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "KnowledgeLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientServiceContractId",
                table: "KnowledgeLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "EmployeeProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "EmployeeProfiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Nss",
                table: "EmployeeProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "EmployeeProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "EmployeeProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "EmployeeProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Curp",
                table: "EmployeeProfiles",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(18)",
                oldMaxLength: 18);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "EmployeeProfiles",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "EmployeeProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankClabe",
                table: "EmployeeProfiles",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "EmployeeProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EducationLevel",
                table: "EmployeeProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "EmployeeProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "EmployeeProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoContentType",
                table: "EmployeeProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoOriginalFileName",
                table: "EmployeeProfiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoStoragePath",
                table: "EmployeeProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rfc",
                table: "EmployeeProfiles",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SatContractTypeCode",
                table: "EmployeeProfiles",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SatJobRiskCode",
                table: "EmployeeProfiles",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SatWorkdayTypeCode",
                table: "EmployeeProfiles",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingEmail",
                table: "Clients",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessLine",
                table: "Clients",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfdiUseCodeDefault",
                table: "Clients",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedToFormalAt",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Clients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalAddress",
                table: "Clients",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalRegimeCode",
                table: "Clients",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiscalZipCode",
                table: "Clients",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemporaryLead",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegalEmail",
                table: "Clients",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalPosition",
                table: "Clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentative",
                table: "Clients",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                table: "Clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Clients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Clients",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutomationReminderLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LogDate = table.Column<DateTime>(type: "date", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationReminderLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientCardDomiciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MercadoPagoPreferenceId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MercadoPagoExternalReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InitPointUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCardDomiciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientCardDomiciliations_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContacts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientLegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientServiceContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    TermsBody = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ContractStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SignatureStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientLegalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientLegalDocuments_ClientServiceContracts_ClientServiceCo~",
                        column: x => x.ClientServiceContractId,
                        principalTable: "ClientServiceContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientLegalDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PasswordProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPortalAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPortalAccesses_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeOrgChartNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportsToUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PositionX = table.Column<int>(type: "integer", nullable: false),
                    PositionY = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOrgChartNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventEmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BodyTemplate = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventEmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentInvestors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestorType = table.Column<int>(type: "integer", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentInvestors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestmentInvestors_EmployeeProfiles_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoginTwoFactorChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginTwoFactorChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PermissionRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Details = table.Column<string>(type: "character varying(1600)", maxLength: 1600, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionAuditLogs_PermissionRoles_PermissionRoleId",
                        column: x => x.PermissionRoleId,
                        principalTable: "PermissionRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PermissionRoleActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionRoleActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionRoleActions_PermissionRoles_PermissionRoleId",
                        column: x => x.PermissionRoleId,
                        principalTable: "PermissionRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PlannedDays = table.Column<int>(type: "integer", nullable: false),
                    DurationUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DurationValue = table.Column<int>(type: "integer", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ColorHex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectActivities_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDeliveryFormats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    ServiceSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EquipmentSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DeliveryLocation = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ReceiverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReceiverEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReceiverPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedByEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SignatureStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeliveryFormats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDeliveryFormats_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDeliveryFormats_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ResellerPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<int>(type: "integer", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResellerPartners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResellerPartners_EmployeeProfiles_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesProspectNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesProspectNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesProspectNotes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesSellerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultCommissionPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesSellerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesSellerProfiles_EmployeeProfiles_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesSipAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Host = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    WsUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SipDomain = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SipUser = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    AuthUser = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SipPasswordProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesSipAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesSipAccounts_EmployeeProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFeasibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SiteAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Coordinates = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SiteContactName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SiteContactPhone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConvertedServiceOrderId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFeasibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFeasibilities_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceFeasibilities_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceFeasibilities_ServiceOrders_ConvertedServiceOrderId",
                        column: x => x.ConvertedServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CompanyLegalName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CompanyRfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    CompanyFiscalRegimeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CompanyFiscalZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompanyFiscalAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    BillingEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CompanyLogoStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompanyLogoOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SmtpHost = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    SmtpUser = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SmtpPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    SmtpFromEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SmtpFromName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SmtpSecurity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SmtpHeloDomain = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SmtpTimeoutMs = table.Column<int>(type: "integer", nullable: false),
                    BillingPacProvider = table.Column<int>(type: "integer", nullable: false),
                    BillingPacApiBaseUrl = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BillingPacApiKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BillingPacApiSecretProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    BillingPacUsername = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    BillingPacPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    CfdiVersion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CfdiSerieDefault = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CsdCerStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CsdKeyStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CsdPasswordProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    MercadoPagoAccessTokenProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    MercadoPagoPublicKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    MercadoPagoWebhookSecretProtected = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    PublicBaseUrl = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ProfitPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    PaymentCount = table.Column<int>(type: "integer", nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestmentPlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvestmentPlans_InvestmentInvestors_InvestorId",
                        column: x => x.InvestorId,
                        principalTable: "InvestmentInvestors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResellerCommissionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PeriodCount = table.Column<int>(type: "integer", nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResellerCommissionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResellerCommissionPlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResellerCommissionPlans_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResellerCommissionPlans_ResellerPartners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "ResellerPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResellerCommissionPlans_ServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BonusDeductionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowStage = table.Column<int>(type: "integer", nullable: false),
                    StageChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StageDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OwnerUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOpportunities_SalesSellerProfiles_SellerProfileId",
                        column: x => x.SellerProfileId,
                        principalTable: "SalesSellerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    PrincipalPortion = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ProfitPortion = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StatementSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestmentPayments_InvestmentPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "InvestmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResellerCommissionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResellerCommissionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResellerCommissionPayments_ResellerCommissionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "ResellerCommissionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingInvoicePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientServiceContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    Concept = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    InvoiceType = table.Column<int>(type: "integer", nullable: false),
                    CfdiUseCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    FiscalRegimeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentFormCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    NextRunDate = table.Column<DateTime>(type: "date", nullable: false),
                    InvoiceIssueDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    RemainingRuns = table.Column<int>(type: "integer", nullable: true),
                    SendToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CcEmails = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoicePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_ClientServiceContracts_ClientServiceCon~",
                        column: x => x.ClientServiceContractId,
                        principalTable: "ClientServiceContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_QuoteRequests_QuoteRequestId",
                        column: x => x.QuoteRequestId,
                        principalTable: "QuoteRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BillingInvoicePlans_SalesOpportunities_SalesOpportunityId",
                        column: x => x.SalesOpportunityId,
                        principalTable: "SalesOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PreviousStage = table.Column<int>(type: "integer", nullable: true),
                    NewStage = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAuditLogs_SalesOpportunities_SalesOpportunityId",
                        column: x => x.SalesOpportunityId,
                        principalTable: "SalesOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesCallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalesOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DialedNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCallLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesCallLogs_EmployeeProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesCallLogs_SalesOpportunities_SalesOpportunityId",
                        column: x => x.SalesOpportunityId,
                        principalTable: "SalesOpportunities",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BillingAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Details = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingAuditLogs_BillingInvoicePlans_BillingPlanId",
                        column: x => x.BillingPlanId,
                        principalTable: "BillingInvoicePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingInvoicePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Concept = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(7,5)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInvoiceLines_BillingInvoicePlans_BillingInvoicePlanId",
                        column: x => x.BillingInvoicePlanId,
                        principalTable: "BillingInvoicePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingInvoiceRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    CfdiUuid = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CfdiStatus = table.Column<int>(type: "integer", nullable: false),
                    CancelReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SatStatusMessage = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    PacTrackingId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "date", nullable: true),
                    PaymentFormCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    PaymentNotes = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingInvoiceRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingInvoiceRuns_BillingInvoicePlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "BillingInvoicePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ClientId",
                table: "KnowledgeLinks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ClientServiceContractId",
                table: "KnowledgeLinks",
                column: "ClientServiceContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsTemporaryLead_CreatedByUserId_CreatedAt",
                table: "Clients",
                columns: new[] { "IsTemporaryLead", "CreatedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsTemporaryLead_IsActive_CreatedAt",
                table: "Clients",
                columns: new[] { "IsTemporaryLead", "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsTemporaryLead_OwnerUserId_CreatedAt",
                table: "Clients",
                columns: new[] { "IsTemporaryLead", "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationReminderLogs_ReminderType_LogDate",
                table: "AutomationReminderLogs",
                columns: new[] { "ReminderType", "LogDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingAuditLogs_BillingPlanId_CreatedAt",
                table: "BillingAuditLogs",
                columns: new[] { "BillingPlanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceLines_BillingInvoicePlanId_SortOrder",
                table: "BillingInvoiceLines",
                columns: new[] { "BillingInvoicePlanId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_ClientId",
                table: "BillingInvoicePlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_ClientServiceContractId",
                table: "BillingInvoicePlans",
                column: "ClientServiceContractId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_QuoteRequestId",
                table: "BillingInvoicePlans",
                column: "QuoteRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_SalesOpportunityId",
                table: "BillingInvoicePlans",
                column: "SalesOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoicePlans_Status_NextRunDate",
                table: "BillingInvoicePlans",
                columns: new[] { "Status", "NextRunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRuns_CfdiUuid",
                table: "BillingInvoiceRuns",
                column: "CfdiUuid");

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRuns_PlanId_ScheduledFor",
                table: "BillingInvoiceRuns",
                columns: new[] { "PlanId", "ScheduledFor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingInvoiceRuns_Status_ScheduledFor",
                table: "BillingInvoiceRuns",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientCardDomiciliations_ClientId_CreatedAt",
                table: "ClientCardDomiciliations",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_Email",
                table: "ClientContacts",
                columns: new[] { "ClientId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_IsPrimary",
                table: "ClientContacts",
                columns: new[] { "ClientId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_Name",
                table: "ClientContacts",
                columns: new[] { "ClientId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_ClientId_DocumentType_Status",
                table: "ClientLegalDocuments",
                columns: new[] { "ClientId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_ClientServiceContractId",
                table: "ClientLegalDocuments",
                column: "ClientServiceContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientLegalDocuments_PublicToken",
                table: "ClientLegalDocuments",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalAccesses_ClientId",
                table: "ClientPortalAccesses",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalAccesses_Username",
                table: "ClientPortalAccesses",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrgChartNodes_ReportsToUserId_SortOrder",
                table: "EmployeeOrgChartNodes",
                columns: new[] { "ReportsToUserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrgChartNodes_UserId",
                table: "EmployeeOrgChartNodes",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventEmailTemplates_EventKey",
                table: "EventEmailTemplates",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentInvestors_Email",
                table: "InvestmentInvestors",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentInvestors_EmployeeUserId",
                table: "InvestmentInvestors",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentInvestors_InvestorType_EmployeeUserId",
                table: "InvestmentInvestors",
                columns: new[] { "InvestorType", "EmployeeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPayments_PlanId_PeriodNumber",
                table: "InvestmentPayments",
                columns: new[] { "PlanId", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlans_ClientId",
                table: "InvestmentPlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentPlans_InvestorId_IsActive",
                table: "InvestmentPlans",
                columns: new[] { "InvestorId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginTwoFactorChallenges_ExpiresAt",
                table: "LoginTwoFactorChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginTwoFactorChallenges_UserId_IpAddress_CreatedAt",
                table: "LoginTwoFactorChallenges",
                columns: new[] { "UserId", "IpAddress", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_CreatedAt",
                table: "PermissionAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_PermissionRoleId",
                table: "PermissionAuditLogs",
                column: "PermissionRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionRoleActions_PermissionRoleId_ActionKey",
                table: "PermissionRoleActions",
                columns: new[] { "PermissionRoleId", "ActionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActivities_ProjectId_SortOrder",
                table: "ProjectActivities",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_ClientId_Status_CreatedAt",
                table: "ProjectDeliveryFormats",
                columns: new[] { "ClientId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_ProjectId",
                table: "ProjectDeliveryFormats",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliveryFormats_PublicToken",
                table: "ProjectDeliveryFormats",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResellerCommissionPayments_PlanId_PeriodNumber",
                table: "ResellerCommissionPayments",
                columns: new[] { "PlanId", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResellerCommissionPlans_ClientId",
                table: "ResellerCommissionPlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ResellerCommissionPlans_PartnerId_IsActive",
                table: "ResellerCommissionPlans",
                columns: new[] { "PartnerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ResellerCommissionPlans_QuoteRequestId",
                table: "ResellerCommissionPlans",
                column: "QuoteRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ResellerCommissionPlans_ServiceOrderId",
                table: "ResellerCommissionPlans",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ResellerPartners_EmployeeUserId",
                table: "ResellerPartners",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResellerPartners_PartyType_EmployeeUserId",
                table: "ResellerPartners",
                columns: new[] { "PartyType", "EmployeeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAuditLogs_SalesOpportunityId_CreatedAt",
                table: "SalesAuditLogs",
                columns: new[] { "SalesOpportunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCallLogs_SalesOpportunityId_CreatedAt",
                table: "SalesCallLogs",
                columns: new[] { "SalesOpportunityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCallLogs_UserId_CreatedAt",
                table: "SalesCallLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_ClientId",
                table: "SalesOpportunities",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_QuoteRequestId",
                table: "SalesOpportunities",
                column: "QuoteRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_SellerProfileId",
                table: "SalesOpportunities",
                column: "SellerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_Status_CreatedAt",
                table: "SalesOpportunities",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOpportunities_WorkflowStage_StageDueAt",
                table: "SalesOpportunities",
                columns: new[] { "WorkflowStage", "StageDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesProspectNotes_ClientId_CreatedAt",
                table: "SalesProspectNotes",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesSellerProfiles_EmployeeUserId",
                table: "SalesSellerProfiles",
                column: "EmployeeUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesSipAccounts_UserId",
                table: "SalesSipAccounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFeasibilities_ClientId_Status_CreatedAt",
                table: "ServiceFeasibilities",
                columns: new[] { "ClientId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFeasibilities_ConvertedServiceOrderId",
                table: "ServiceFeasibilities",
                column: "ConvertedServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFeasibilities_ProjectId",
                table: "ServiceFeasibilities",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigurations_UpdatedAt",
                table: "SystemConfigurations",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContract~",
                table: "KnowledgeLinks",
                column: "ClientServiceContractId",
                principalTable: "ClientServiceContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeLinks_Clients_ClientId",
                table: "KnowledgeLinks",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContract~",
                table: "KnowledgeLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeLinks_Clients_ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropTable(
                name: "AutomationReminderLogs");

            migrationBuilder.DropTable(
                name: "BillingAuditLogs");

            migrationBuilder.DropTable(
                name: "BillingInvoiceLines");

            migrationBuilder.DropTable(
                name: "BillingInvoiceRuns");

            migrationBuilder.DropTable(
                name: "ClientCardDomiciliations");

            migrationBuilder.DropTable(
                name: "ClientContacts");

            migrationBuilder.DropTable(
                name: "ClientLegalDocuments");

            migrationBuilder.DropTable(
                name: "ClientPortalAccesses");

            migrationBuilder.DropTable(
                name: "EmployeeOrgChartNodes");

            migrationBuilder.DropTable(
                name: "EventEmailTemplates");

            migrationBuilder.DropTable(
                name: "InvestmentPayments");

            migrationBuilder.DropTable(
                name: "LoginTwoFactorChallenges");

            migrationBuilder.DropTable(
                name: "PermissionAuditLogs");

            migrationBuilder.DropTable(
                name: "PermissionRoleActions");

            migrationBuilder.DropTable(
                name: "ProjectActivities");

            migrationBuilder.DropTable(
                name: "ProjectDeliveryFormats");

            migrationBuilder.DropTable(
                name: "ResellerCommissionPayments");

            migrationBuilder.DropTable(
                name: "SalesAuditLogs");

            migrationBuilder.DropTable(
                name: "SalesCallLogs");

            migrationBuilder.DropTable(
                name: "SalesProspectNotes");

            migrationBuilder.DropTable(
                name: "SalesSipAccounts");

            migrationBuilder.DropTable(
                name: "ServiceFeasibilities");

            migrationBuilder.DropTable(
                name: "SystemConfigurations");

            migrationBuilder.DropTable(
                name: "BillingInvoicePlans");

            migrationBuilder.DropTable(
                name: "InvestmentPlans");

            migrationBuilder.DropTable(
                name: "ResellerCommissionPlans");

            migrationBuilder.DropTable(
                name: "SalesOpportunities");

            migrationBuilder.DropTable(
                name: "InvestmentInvestors");

            migrationBuilder.DropTable(
                name: "ResellerPartners");

            migrationBuilder.DropTable(
                name: "SalesSellerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_ClientServiceContractId",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsTemporaryLead_CreatedByUserId_CreatedAt",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsTemporaryLead_IsActive_CreatedAt",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_IsTemporaryLead_OwnerUserId_CreatedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ContractTermMonths",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "GeneralTerms",
                table: "QuoteRequests");

            migrationBuilder.DropColumn(
                name: "Recurrence",
                table: "QuoteRequestLines");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ClientServiceContractId",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "BankClabe",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "EducationLevel",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoContentType",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoOriginalFileName",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoStoragePath",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "Rfc",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "SatContractTypeCode",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "SatJobRiskCode",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "SatWorkdayTypeCode",
                table: "EmployeeProfiles");

            migrationBuilder.DropColumn(
                name: "BillingEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BusinessLine",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CfdiUseCodeDefault",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ConvertedToFormalAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalAddress",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalRegimeCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FiscalZipCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsTemporaryLead",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalEmail",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalPosition",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LegalRepresentative",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Municipality",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Clients");

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "EmployeeProfiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "EmployeeProfiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nss",
                table: "EmployeeProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "EmployeeProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "EmployeeProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "EmployeeProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Curp",
                table: "EmployeeProfiles",
                type: "character varying(18)",
                maxLength: 18,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(18)",
                oldMaxLength: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "EmployeeProfiles",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldNullable: true);
        }
    }
}
