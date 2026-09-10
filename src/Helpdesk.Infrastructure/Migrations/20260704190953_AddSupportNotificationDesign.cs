using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportNotificationDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportGroups",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwningOrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportNotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TicketTrackingId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportNotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportNotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerOrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    RecipientType = table.Column<int>(type: "integer", nullable: false),
                    RecipientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportNotificationSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSupportNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSupportNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSupportCoverages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerOrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderOrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SupportGroupId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupportGroupId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_CustomerOrganizationId_IsEnabl~",
                table: "OrganizationSupportCoverages",
                columns: new[] { "CustomerOrganizationId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_CustomerOrganizationId_Provide~",
                table: "OrganizationSupportCoverages",
                columns: new[] { "CustomerOrganizationId", "ProviderOrganizationId", "SupportGroupId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSupportCoverages_SupportGroupId_IsEnabled",
                table: "OrganizationSupportCoverages",
                columns: new[] { "SupportGroupId", "IsEnabled" });

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
                name: "IX_SupportNotificationSubscriptions_CustomerOrganizationId_Eve~",
                table: "SupportNotificationSubscriptions",
                columns: new[] { "CustomerOrganizationId", "EventType", "Channel", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportNotificationSubscriptions_RecipientType_RecipientId",
                table: "SupportNotificationSubscriptions",
                columns: new[] { "RecipientType", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSupportNotificationPreferences_UserId_EventType_Channel",
                table: "UserSupportNotificationPreferences",
                columns: new[] { "UserId", "EventType", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationSupportCoverages");

            migrationBuilder.DropTable(
                name: "SupportGroupMembers");

            migrationBuilder.DropTable(
                name: "SupportNotificationDeliveries");

            migrationBuilder.DropTable(
                name: "SupportNotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "UserSupportNotificationPreferences");

            migrationBuilder.DropTable(
                name: "SupportGroups");
        }
    }
}
