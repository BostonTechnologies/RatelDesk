using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk
{
    /// <inheritdoc />
    public partial class InitialSqliteApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: true),
                    RelatedEntityId = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantChatConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketType = table.Column<string>(type: "TEXT", nullable: false),
                    AiAssistantSessionId = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    ActiveMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastActivityUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TurnStartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastTransportActivityAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantWebhookAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InvocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantWebhookAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantWebhookConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    TicketAreasJson = table.Column<string>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    RouteName = table.Column<string>(type: "TEXT", nullable: true),
                    PromptTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    PermittedInputsSchemaJson = table.Column<string>(type: "TEXT", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxBodyBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    SecretReference = table.Column<string>(type: "TEXT", nullable: true),
                    SigningSecretProtected = table.Column<string>(type: "TEXT", nullable: true),
                    LastDispatchUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastHealthMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantWebhookConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiInvestigationInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketArea = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OperatorAssistanceRequest = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AiAssistantRunReference = table.Column<string>(type: "TEXT", nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: true),
                    DispatchAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DispatchedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvestigationInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiInvestigationWorklogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketArea = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    ArtifactReferencesJson = table.Column<string>(type: "TEXT", nullable: true),
                    CallbackEventId = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvestigationWorklogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiOperationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SubjectId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiOperationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultModel = table.Column<string>(type: "TEXT", nullable: true),
                    ExtraHeadersJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedById = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationBindings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RequestFormId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TaskTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrchestrationRequestDefinitionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OrchestrationRequestDefinitionName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OrchestrationJobDefinitionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OrchestrationJobDefinitionName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SyncState = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncDirection = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastReviewedDriftAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastCorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    Conditions = table.Column<string>(type: "TEXT", nullable: true),
                    Actions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockedEntities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Domain = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    BlockedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetColumns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsKey = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDisplay = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSearchable = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetColumns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    KeyColumn = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayColumn = table.Column<string>(type: "TEXT", nullable: false),
                    SearchColumnsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetIngestCredentials",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetIngestCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatasetRows",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalKey = table.Column<string>(type: "TEXT", nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    RowHash = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastIngestedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailInboxSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MailHost = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    MailboxAddress = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientSecret = table.Column<string>(type: "TEXT", nullable: false),
                    MailboxFolder = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackgroundSyncEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailInboxSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    HtmlContent = table.Column<string>(type: "TEXT", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HangfireRuntimeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HangfireRuntimeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImapEmailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    Mailbox = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientSecret = table.Column<string>(type: "TEXT", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedFolder = table.Column<string>(type: "TEXT", nullable: false),
                    BlockedFolder = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastTestStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImapEmailSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundEmailProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    MailboxId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MailboxKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RuleId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Matched = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailProcessingLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundEmailRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MailboxId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    ActionsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    StopProcessing = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEmailRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstanceBrandings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationName = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: true),
                    ApplicationUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizationUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SupportUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SupportEmail = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CompactLogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FaviconUrl = table.Column<string>(type: "TEXT", nullable: true),
                    EmailFromDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceBrandings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    Service = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Problem = table.Column<string>(type: "TEXT", nullable: true),
                    Resolution = table.Column<string>(type: "TEXT", nullable: true),
                    RootCause = table.Column<string>(type: "TEXT", nullable: true),
                    Application = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    SourceIncidentId = table.Column<string>(type: "TEXT", nullable: true),
                    AutomationBindingId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AutomationRequestFormId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AutomationTaskTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AutomationTaskTemplateName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AutomationOrchestrationRequestDefinitionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AutomationOrchestrationJobDefinitionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Draft"),
                    LinkedTicketId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByModel = table.Column<string>(type: "TEXT", nullable: true),
                    LastRegeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PublishedById = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "M2MConnectivitySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemoteBaseUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RemoteAudience = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RemoteSystemName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RemoteTokenEndpoint = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RemoteAuthority = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_M2MConnectivitySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Link = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsGlobal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAiKbSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    EnableAiSearch = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableAiAnswers = table.Column<bool>(type: "INTEGER", nullable: false),
                    SearchThreshold = table.Column<double>(type: "REAL", nullable: false),
                    AnswerThreshold = table.Column<double>(type: "REAL", nullable: false),
                    EmbeddingProviderId = table.Column<string>(type: "TEXT", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    KnowledgeProviderId = table.Column<string>(type: "TEXT", nullable: true),
                    KnowledgeModelName = table.Column<string>(type: "TEXT", nullable: true),
                    SuggestionLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowedServicesCsv = table.Column<string>(type: "TEXT", nullable: true),
                    EnableProviderFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxProviderAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumSuggestionFeedbackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumSuggestionHelpfulRate = table.Column<double>(type: "REAL", nullable: false),
                    MinimumAutomationFeedbackCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumAutomationResolvedRate = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAiKbSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DnsName = table.Column<string>(type: "TEXT", nullable: true),
                    EnableAiIntake = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContactInfo = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedSlaId = table.Column<string>(type: "TEXT", nullable: true),
                    ItSupportOrganizationId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationTenantId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrchestrationTenantName = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationTenantLinkedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_Organizations_ItSupportOrganizationId",
                        column: x => x.ItSupportOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestForms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    JsonSchema = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedOrganizationIds = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerOrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScopedRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RoleKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopedRoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ParentServiceId = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedCustomerIds = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedOrganizationIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    AppliesTo = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: true),
                    MatchRank = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolutionTimeHours = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoResumeAfterHours = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaReportSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeklyDay = table.Column<int>(type: "INTEGER", nullable: true),
                    SendTimeLocal = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    LookbackDays = table.Column<int>(type: "INTEGER", nullable: false),
                    IncludeCsvAttachment = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeExcelAttachment = table.Column<bool>(type: "INTEGER", nullable: false),
                    TicketType = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaReportSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportGroups",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OwningOrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportNotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TicketTrackingId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RecipientEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SentUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportNotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportNotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerOrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientType = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportNotificationSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBrandings",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BrandName = table.Column<string>(type: "TEXT", nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FooterHtml = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryColor = table.Column<string>(type: "TEXT", nullable: false),
                    FromName = table.Column<string>(type: "TEXT", nullable: false),
                    ReplyTo = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBrandings", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "TenantGraphDatasetSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", nullable: true),
                    ClientSecretProtected = table.Column<string>(type: "TEXT", nullable: true),
                    EnableUsers = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableDevices = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableSharePointSites = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackgroundSyncEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    BackgroundSyncCronExpression = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsersSyncUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastDevicesSyncUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastGroupsSyncUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSharePointSitesSyncUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantGraphDatasetSettings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "TenantSlaSettings",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    UseBusinessHours = table.Column<bool>(type: "INTEGER", nullable: false),
                    CalendarId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSlaSettings", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentCategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCategories_TicketCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    Comments = table.Column<string>(type: "TEXT", nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceTicketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetTicketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RelationType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRelations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssignedToId = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: true),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedAssetIds = table.Column<string>(type: "TEXT", nullable: false),
                    SlaPolicyId = table.Column<string>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EmailExclusionReason = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Attachments = table.Column<string>(type: "TEXT", nullable: false),
                    RequesterEmail = table.Column<string>(type: "TEXT", nullable: true),
                    CcRecipientsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    LastViewedByCustomerAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TrackingId = table.Column<string>(type: "TEXT", nullable: false),
                    AiUnderstanding = table.Column<string>(type: "TEXT", nullable: true),
                    Replies = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSpentHours = table.Column<double>(type: "REAL", nullable: false),
                    LastReplierName = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LifecycleState = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 0),
                    RequestedForUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ImplementorUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ApproverUserIdsJson = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "'[]'"),
                    ImplementationStartAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImplementationEndAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangeTemplateJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiReviewStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    AiReviewGateState = table.Column<int>(type: "INTEGER", nullable: true),
                    AiReviewOutputJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiReviewCorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AiReviewFailureReason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AiReviewAcknowledgementNotes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AiReviewRequestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AiReviewCompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AiReviewAcknowledgedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AiReviewAcknowledgedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AiReviewAcknowledgedByName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Impact = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalEmailHtml = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalEmailText = table.Column<string>(type: "TEXT", nullable: true),
                    EmailFrom = table.Column<string>(type: "TEXT", nullable: true),
                    EmailReceivedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    SourceTicketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceKnowledgeArticleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceAutomationBindingId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RequestFormId = table.Column<string>(type: "TEXT", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowStatus = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowBlockReason = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RequestId = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateId = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TaskSlaMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    EscalateAfterMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    EscalationUserId = table.Column<string>(type: "TEXT", nullable: true),
                    EscalationRole = table.Column<string>(type: "TEXT", nullable: true),
                    SlaStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Escalated = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SlaBreached = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: false),
                    UnblockedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConditionExpression = table.Column<string>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 0),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    IsCritical = table.Column<bool>(type: "INTEGER", nullable: true),
                    FailurePolicy = table.Column<string>(type: "TEXT", nullable: true),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: true),
                    RetryDelayMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    AutomationBindingId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationRequestDefinitionId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationJobDefinitionId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationExternalRequestId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestrationExternalRunId = table.Column<string>(type: "TEXT", nullable: true),
                    LastAutomationStatus = table.Column<string>(type: "TEXT", nullable: true),
                    LastAutomationUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExpectedRuntimeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    GraceSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    HardTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    TimeoutIncidentId = table.Column<string>(type: "TEXT", nullable: true),
                    OrchestratorExecutionId = table.Column<string>(type: "TEXT", nullable: true),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Tickets_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageHtml = table.Column<string>(type: "TEXT", nullable: true),
                    MessageText = table.Column<string>(type: "TEXT", nullable: true),
                    EmailStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    EmailRecipient = table.Column<string>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRetryUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RetryError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTimelineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: true),
                    HashedPassword = table.Column<string>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    IsTestUser = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSupportNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSupportNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkingCalendars",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: false),
                    WeeklyRulesJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    ExceptionsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    Hours = table.Column<double>(type: "REAL", nullable: false),
                    IsInternalNote = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotesHtml = table.Column<string>(type: "TEXT", nullable: true),
                    NotesText = table.Column<string>(type: "TEXT", nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TechnicianId = table.Column<string>(type: "TEXT", nullable: true),
                    SeenByCustomerAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantChatEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CallId = table.Column<string>(type: "TEXT", nullable: true),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAssistantChatEvents_AiAssistantChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiAssistantChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAssistantChatInteractions",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CallId = table.Column<string>(type: "TEXT", nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedKey = table.Column<string>(type: "TEXT", nullable: true),
                    AnsweredByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAssistantChatInteractions", x => new { x.ConversationId, x.CallId });
                    table.ForeignKey(
                        name: "FK_AiAssistantChatInteractions_AiAssistantChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiAssistantChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AiProviderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtraHeadersJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiModels_AiProviders_AiProviderId",
                        column: x => x.AiProviderId,
                        principalTable: "AiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAuthLinks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthProviderType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OidcIssuer = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    OidcSubject = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AuthentikUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AuthentikUsername = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AuthentikEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    InviteStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    InviteSentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    InviteAcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    InvitedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DisabledByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    LastAuthSyncAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastAuthError = table.Column<string>(type: "TEXT", nullable: true),
                    InviteLinkExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAuthLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAuthLinks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    HtmlContent = table.Column<string>(type: "TEXT", nullable: false),
                    LayoutId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_EmailLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalTable: "EmailLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ChunkIndex = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ChunkId = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastScore = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    KnowledgeBaseArticleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Vector = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeEmbeddings_KnowledgeBaseArticles_KnowledgeBaseArticleId",
                        column: x => x.KnowledgeBaseArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TicketKnowledgeSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    KnowledgeBaseArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketKnowledgeSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketKnowledgeSuggestions_KnowledgeBaseArticles_KnowledgeBaseArticleId",
                        column: x => x.KnowledgeBaseArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => new { x.NotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaEscalationRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PolicyId = table.Column<string>(type: "TEXT", nullable: false),
                    Metric = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    TargetsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValueSql: "'[]'"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaEscalationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaEscalationRules_SlaPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaReportSendEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaReportSendEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaReportSendEvents_SlaReportSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "SlaReportSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSupportCoverages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerOrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderOrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SupportGroupId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSupportCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationSupportCoverages_SupportGroups_SupportGroupId",
                        column: x => x.SupportGroupId,
                        principalTable: "SupportGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportGroupMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SupportGroupId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportGroupMembers_SupportGroups_SupportGroupId",
                        column: x => x.SupportGroupId,
                        principalTable: "SupportGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeId = table.Column<string>(type: "TEXT", nullable: false),
                    ApproverId = table.Column<string>(type: "TEXT", nullable: true),
                    ApproverName = table.Column<string>(type: "TEXT", nullable: false),
                    ApproverEmail = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TokenSentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeApprovals_Tickets_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeCategoryLinks",
                columns: table => new
                {
                    ChangeId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeCategoryLinks", x => new { x.ChangeId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_ChangeCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeCategoryLinks_Tickets_ChangeId",
                        column: x => x.ChangeId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentCategoryLinks",
                columns: table => new
                {
                    IncidentId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentCategoryLinks", x => new { x.IncidentId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_IncidentCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentCategoryLinks_Tickets_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestCategoryLinks",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "TEXT", nullable: false),
                    TicketCategoryId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCategoryLinks", x => new { x.RequestId, x.TicketCategoryId });
                    table.ForeignKey(
                        name: "FK_RequestCategoryLinks_TicketCategories_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestCategoryLinks_Tickets_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestTaskApprovals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RequestId = table.Column<string>(type: "TEXT", nullable: false),
                    RequestTaskId = table.Column<string>(type: "TEXT", nullable: false),
                    ApproverSource = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ApproverId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ApproverName = table.Column<string>(type: "TEXT", nullable: false),
                    ApproverEmail = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OrganizationName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TokenSentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTaskApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTaskApprovals_Tickets_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestTaskApprovals_Tickets_RequestTaskId",
                        column: x => x.RequestTaskId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketAiFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    FeedbackType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FeedbackValue = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ArticleId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RequestId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedByName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAiFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAiFeedback_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketAiSuggestions",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAiSuggestions", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_TicketAiSuggestions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketSlaEscalationEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    Metric = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PolicyId = table.Column<string>(type: "TEXT", nullable: false),
                    RecipientsCsv = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: true),
                    SendStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSlaEscalationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSlaEscalationEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketSlaStates",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResponseDueAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolutionDueAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PausedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AccumulatedPauseDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    AccumulatedPauseWorkingSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    PauseReason = table.Column<string>(type: "TEXT", nullable: true),
                    PausedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ResumeAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastResumedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsBusinessHours = table.Column<bool>(type: "INTEGER", nullable: false),
                    CalendarId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseBreached = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolutionBreached = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedWithinResponseSla = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedWithinResolutionSla = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSlaStates", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_TicketSlaStates_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatConversations_OrganizationId_TicketType_TicketId",
                table: "AiAssistantChatConversations",
                columns: new[] { "OrganizationId", "TicketType", "TicketId" },
                unique: true,
                filter: "\"State\" <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatConversations_State_LastTransportActivityAtUtc",
                table: "AiAssistantChatConversations",
                columns: new[] { "State", "LastTransportActivityAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatEvents_ConversationId_ClientMessageId",
                table: "AiAssistantChatEvents",
                columns: new[] { "ConversationId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantChatEvents_ConversationId_Sequence",
                table: "AiAssistantChatEvents",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookAuditRecords_OrganizationId_CreatedUtc",
                table: "AiAssistantWebhookAuditRecords",
                columns: new[] { "OrganizationId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookConfigurations_OrganizationId_IsEnabled_IsArchived",
                table: "AiAssistantWebhookConfigurations",
                columns: new[] { "OrganizationId", "IsEnabled", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssistantWebhookConfigurations_OrganizationId_Name",
                table: "AiAssistantWebhookConfigurations",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_CorrelationId",
                table: "AiInvestigationInvocations",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_OrganizationId_IdempotencyKey",
                table: "AiInvestigationInvocations",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationInvocations_OrganizationId_TicketArea_TicketId_CreatedUtc",
                table: "AiInvestigationInvocations",
                columns: new[] { "OrganizationId", "TicketArea", "TicketId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationWorklogEntries_CallbackEventId",
                table: "AiInvestigationWorklogEntries",
                column: "CallbackEventId",
                unique: true,
                filter: "\"CallbackEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvestigationWorklogEntries_OrganizationId_TicketArea_TicketId_OccurredUtc",
                table: "AiInvestigationWorklogEntries",
                columns: new[] { "OrganizationId", "TicketArea", "TicketId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_AiProviderId",
                table: "AiModels",
                column: "AiProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationAuditRecords_OrganizationId_CreatedAt",
                table: "AiOperationAuditRecords",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationAuditRecords_SubjectId_CreatedAt",
                table: "AiOperationAuditRecords",
                columns: new[] { "SubjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationBindings_OrganizationId_OrchestrationRequestDefinitionId",
                table: "AutomationBindings",
                columns: new[] { "OrganizationId", "OrchestrationRequestDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationBindings_OrganizationId_RequestFormId_TaskTemplateId",
                table: "AutomationBindings",
                columns: new[] { "OrganizationId", "RequestFormId", "TaskTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ApproverEmail",
                table: "ChangeApprovals",
                column: "ApproverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ChangeId",
                table: "ChangeApprovals",
                column: "ChangeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ChangeId_ApproverEmail",
                table: "ChangeApprovals",
                columns: new[] { "ChangeId", "ApproverEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_Status",
                table: "ChangeApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApprovals_ViewedAtUtc",
                table: "ChangeApprovals",
                column: "ViewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeCategoryLinks_TicketCategoryId",
                table: "ChangeCategoryLinks",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_AuthentikUserId",
                table: "CustomerAuthLinks",
                column: "AuthentikUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_CustomerId",
                table: "CustomerAuthLinks",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_InviteStatus_InviteSentAtUtc",
                table: "CustomerAuthLinks",
                columns: new[] { "InviteStatus", "InviteSentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuthLinks_OidcIssuer_OidcSubject",
                table: "CustomerAuthLinks",
                columns: new[] { "OidcIssuer", "OidcSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetColumns_DatasetId_Name",
                table: "DatasetColumns",
                columns: new[] { "DatasetId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetDefinitions_OrganizationId_Slug",
                table: "DatasetDefinitions",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetDefinitions_OrganizationId_SourceType",
                table: "DatasetDefinitions",
                columns: new[] { "OrganizationId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetIngestCredentials_DatasetId_KeyHash",
                table: "DatasetIngestCredentials",
                columns: new[] { "DatasetId", "KeyHash" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetIngestCredentials_DatasetId_Name",
                table: "DatasetIngestCredentials",
                columns: new[] { "DatasetId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DatasetRows_DatasetId_ExternalKey",
                table: "DatasetRows",
                columns: new[] { "DatasetId", "ExternalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatasetRows_DatasetId_OrganizationId",
                table: "DatasetRows",
                columns: new[] { "DatasetId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInboxSettings_MailboxAddress",
                table: "EmailInboxSettings",
                column: "MailboxAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLayouts_Name_TenantId",
                table: "EmailLayouts",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_LayoutId",
                table: "EmailTemplates",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_MessageId_MailboxKey_RuleId_ActionKey",
                table: "InboundEmailProcessingLogs",
                columns: new[] { "MessageId", "MailboxKey", "RuleId", "ActionKey" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_MessageId_MailboxKey_RuleId_ActionKey_Status",
                table: "InboundEmailProcessingLogs",
                columns: new[] { "MessageId", "MailboxKey", "RuleId", "ActionKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailProcessingLogs_TicketId",
                table: "InboundEmailProcessingLogs",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundEmailRules_Enabled_ScopeType_TenantId_MailboxId_Priority",
                table: "InboundEmailRules",
                columns: new[] { "Enabled", "ScopeType", "TenantId", "MailboxId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentCategoryLinks_TicketCategoryId",
                table: "IncidentCategoryLinks",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId",
                table: "KnowledgeBaseArticles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_OrganizationId_AutomationBindingId",
                table: "KnowledgeBaseArticles",
                columns: new[] { "OrganizationId", "AutomationBindingId" });

            migrationBuilder.CreateIndex(
                name: "ix_embedding_org_document_chunk",
                table: "KnowledgeEmbeddings",
                columns: new[] { "OrganizationId", "SourceType", "SourceId", "ChunkIndex" });

            migrationBuilder.CreateIndex(
                name: "ix_embedding_org_source",
                table: "KnowledgeEmbeddings",
                columns: new[] { "OrganizationId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEmbeddings_KnowledgeBaseArticleId",
                table: "KnowledgeEmbeddings",
                column: "KnowledgeBaseArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_UserId",
                table: "NotificationReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Category",
                table: "Notifications",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Category_CreatedUtc",
                table: "Notifications",
                columns: new[] { "Category", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CorrelationId",
                table: "Notifications",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedUtc",
                table: "Notifications",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsGlobal",
                table: "Notifications",
                column: "IsGlobal");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Reference",
                table: "Notifications",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_Category",
                table: "Notifications",
                columns: new[] { "TenantId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_DnsName",
                table: "Organizations",
                column: "DnsName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ItSupportOrganizationId",
                table: "Organizations",
                column: "ItSupportOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_CustomerOrganizationId_IsEnabled",
                table: "OrganizationSupportCoverages",
                columns: new[] { "CustomerOrganizationId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_CustomerOrganizationId_ProviderOrganizationId_SupportGroupId_Role",
                table: "OrganizationSupportCoverages",
                columns: new[] { "CustomerOrganizationId", "ProviderOrganizationId", "SupportGroupId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_SupportGroupId_IsEnabled",
                table: "OrganizationSupportCoverages",
                columns: new[] { "SupportGroupId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestCategoryLinks_TicketCategoryId",
                table: "RequestCategoryLinks",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_ApproverEmail",
                table: "RequestTaskApprovals",
                column: "ApproverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestId",
                table: "RequestTaskApprovals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestTaskId",
                table: "RequestTaskApprovals",
                column: "RequestTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_RequestTaskId_ApproverEmail",
                table: "RequestTaskApprovals",
                columns: new[] { "RequestTaskId", "ApproverEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_Status",
                table: "RequestTaskApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTaskApprovals_ViewedAtUtc",
                table: "RequestTaskApprovals",
                column: "ViewedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Key",
                table: "Roles",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Scope_OwnerOrganizationId",
                table: "Roles",
                columns: new[] { "Scope", "OwnerOrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_OrganizationId_RoleKey",
                table: "ScopedRoleAssignments",
                columns: new[] { "OrganizationId", "RoleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_UserId_RoleKey_OrganizationId",
                table: "ScopedRoleAssignments",
                columns: new[] { "UserId", "RoleKey", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaEscalationRules_PolicyId_Metric_TriggerPercent",
                table: "SlaEscalationRules",
                columns: new[] { "PolicyId", "Metric", "TriggerPercent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_ScopeType_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "ScopeType", "AppliesTo", "ServiceId", "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_TenantId_AppliesTo_ServiceId_Priority_IsActive",
                table: "SlaPolicies",
                columns: new[] { "TenantId", "AppliesTo", "ServiceId", "Priority", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSendEvents_SubscriptionId_PeriodStartUtc_PeriodEndUtc",
                table: "SlaReportSendEvents",
                columns: new[] { "SubscriptionId", "PeriodStartUtc", "PeriodEndUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSubscriptions_Frequency_IsActive",
                table: "SlaReportSubscriptions",
                columns: new[] { "Frequency", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SlaReportSubscriptions_TenantId_IsActive",
                table: "SlaReportSubscriptions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportGroupMembers_SupportGroupId_UserId",
                table: "SupportGroupMembers",
                columns: new[] { "SupportGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportGroupMembers_UserId_IsEnabled",
                table: "SupportGroupMembers",
                columns: new[] { "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportGroups_OwningOrganizationId_IsEnabled",
                table: "SupportGroups",
                columns: new[] { "OwningOrganizationId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportGroups_OwningOrganizationId_Name",
                table: "SupportGroups",
                columns: new[] { "OwningOrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationDeliveries_DeduplicationKey",
                table: "SupportNotificationDeliveries",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationDeliveries_Status_CreatedUtc",
                table: "SupportNotificationDeliveries",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationDeliveries_TicketId_EventType",
                table: "SupportNotificationDeliveries",
                columns: new[] { "TicketId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationSubscriptions_CustomerOrganizationId_EventType_Channel_IsEnabled",
                table: "SupportNotificationSubscriptions",
                columns: new[] { "CustomerOrganizationId", "EventType", "Channel", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationSubscriptions_RecipientType_RecipientId",
                table: "SupportNotificationSubscriptions",
                columns: new[] { "RecipientType", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSlaSettings_CalendarId",
                table: "TenantSlaSettings",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_ArticleId_FeedbackType_FeedbackValue",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "ArticleId", "FeedbackType", "FeedbackValue" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_CreatedAt",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAiFeedback_TicketId_RequestId_FeedbackType_FeedbackValue",
                table: "TicketAiFeedback",
                columns: new[] { "TicketId", "RequestId", "FeedbackType", "FeedbackValue" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_Name_Type_TenantId",
                table: "TicketCategories",
                columns: new[] { "Name", "Type", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_ParentCategoryId",
                table: "TicketCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_Type_TenantId_SortOrder",
                table: "TicketCategories",
                columns: new[] { "Type", "TenantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketKnowledgeSuggestions_KnowledgeBaseArticleId",
                table: "TicketKnowledgeSuggestions",
                column: "KnowledgeBaseArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_SourceTicketId",
                table: "TicketRelations",
                column: "SourceTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_SourceTicketId_TargetTicketId_RelationType",
                table: "TicketRelations",
                columns: new[] { "SourceTicketId", "TargetTicketId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRelations_TargetTicketId",
                table: "TicketRelations",
                column: "TargetTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Escalated_Status",
                table: "Tickets",
                columns: new[] { "Escalated", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ImplementorUserId",
                table: "Tickets",
                column: "ImplementorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_ClosedAt",
                table: "Tickets",
                columns: new[] { "OrganizationId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_SourceKnowledgeArticleId",
                table: "Tickets",
                columns: new[] { "OrganizationId", "SourceKnowledgeArticleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OrganizationId_SourceTicketId",
                table: "Tickets",
                columns: new[] { "OrganizationId", "SourceTicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestedForUserId",
                table: "Tickets",
                column: "RequestedForUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestId",
                table: "Tickets",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestId_IsBlocked",
                table: "Tickets",
                columns: new[] { "RequestId", "IsBlocked" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_State",
                table: "Tickets",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status",
                table: "Tickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_DueAt",
                table: "Tickets",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_NextRetryAt",
                table: "Tickets",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaEscalationEvents_TicketId_Metric_TriggerPercent",
                table: "TicketSlaEscalationEvents",
                columns: new[] { "TicketId", "Metric", "TriggerPercent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_CalendarId",
                table: "TicketSlaStates",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_ResolutionDueAt",
                table: "TicketSlaStates",
                column: "ResolutionDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_ResumeAt",
                table: "TicketSlaStates",
                column: "ResumeAt");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_Status",
                table: "TicketSlaStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSlaStates_Status_CompletedAt",
                table: "TicketSlaStates",
                columns: new[] { "Status", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSupportNotificationPreferences_UserId_EventType_Channel",
                table: "UserSupportNotificationPreferences",
                columns: new[] { "UserId", "EventType", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_ScopeType_IsActive",
                table: "WorkingCalendars",
                columns: new[] { "ScopeType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_TenantId_IsActive",
                table: "WorkingCalendars",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "AiAssistantChatEvents");

            migrationBuilder.DropTable(
                name: "AiAssistantChatInteractions");

            migrationBuilder.DropTable(
                name: "AiAssistantWebhookAuditRecords");

            migrationBuilder.DropTable(
                name: "AiAssistantWebhookConfigurations");

            migrationBuilder.DropTable(
                name: "AiInvestigationInvocations");

            migrationBuilder.DropTable(
                name: "AiInvestigationWorklogEntries");

            migrationBuilder.DropTable(
                name: "AiModels");

            migrationBuilder.DropTable(
                name: "AiOperationAuditRecords");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "AutomationBindings");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "BlockedEntities");

            migrationBuilder.DropTable(
                name: "ChangeApprovals");

            migrationBuilder.DropTable(
                name: "ChangeCategoryLinks");

            migrationBuilder.DropTable(
                name: "CustomerAuthLinks");

            migrationBuilder.DropTable(
                name: "DatasetColumns");

            migrationBuilder.DropTable(
                name: "DatasetDefinitions");

            migrationBuilder.DropTable(
                name: "DatasetIngestCredentials");

            migrationBuilder.DropTable(
                name: "DatasetRows");

            migrationBuilder.DropTable(
                name: "EmailInboxSettings");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "HangfireRuntimeSettings");

            migrationBuilder.DropTable(
                name: "ImapEmailSettings");

            migrationBuilder.DropTable(
                name: "InboundEmailProcessingLogs");

            migrationBuilder.DropTable(
                name: "InboundEmailRules");

            migrationBuilder.DropTable(
                name: "IncidentCategoryLinks");

            migrationBuilder.DropTable(
                name: "InstanceBrandings");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseCategories");

            migrationBuilder.DropTable(
                name: "KnowledgeEmbeddings");

            migrationBuilder.DropTable(
                name: "M2MConnectivitySettings");

            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "OrganizationAiKbSettings");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "OrganizationSupportCoverages");

            migrationBuilder.DropTable(
                name: "RequestCategoryLinks");

            migrationBuilder.DropTable(
                name: "RequestForms");

            migrationBuilder.DropTable(
                name: "RequestTaskApprovals");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScopedRoleAssignments");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "SlaEscalationRules");

            migrationBuilder.DropTable(
                name: "SlaReportSendEvents");

            migrationBuilder.DropTable(
                name: "SupportGroupMembers");

            migrationBuilder.DropTable(
                name: "SupportNotificationDeliveries");

            migrationBuilder.DropTable(
                name: "SupportNotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "TenantBrandings");

            migrationBuilder.DropTable(
                name: "TenantGraphDatasetSettings");

            migrationBuilder.DropTable(
                name: "TenantSlaSettings");

            migrationBuilder.DropTable(
                name: "TicketAiFeedback");

            migrationBuilder.DropTable(
                name: "TicketAiSuggestions");

            migrationBuilder.DropTable(
                name: "TicketEvents");

            migrationBuilder.DropTable(
                name: "TicketKnowledgeSuggestions");

            migrationBuilder.DropTable(
                name: "TicketRelations");

            migrationBuilder.DropTable(
                name: "TicketSlaEscalationEvents");

            migrationBuilder.DropTable(
                name: "TicketSlaStates");

            migrationBuilder.DropTable(
                name: "TicketTimelineEvents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "UserSupportNotificationPreferences");

            migrationBuilder.DropTable(
                name: "WorkingCalendars");

            migrationBuilder.DropTable(
                name: "WorkLogs");

            migrationBuilder.DropTable(
                name: "AiAssistantChatConversations");

            migrationBuilder.DropTable(
                name: "AiProviders");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "EmailLayouts");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "TicketCategories");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "SlaReportSubscriptions");

            migrationBuilder.DropTable(
                name: "SupportGroups");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticles");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
