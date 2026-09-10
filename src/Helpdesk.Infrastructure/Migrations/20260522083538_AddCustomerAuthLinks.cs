using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAuthLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAuthLinks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    AuthProviderType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OidcIssuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OidcSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AuthentikUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AuthentikUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AuthentikEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    InviteStatus = table.Column<int>(type: "integer", nullable: false),
                    InviteSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InviteAcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvitedByUserId = table.Column<string>(type: "text", nullable: true),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisabledByUserId = table.Column<string>(type: "text", nullable: true),
                    LastAuthSyncAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAuthError = table.Column<string>(type: "text", nullable: true),
                    InviteLinkExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAuthLinks");
        }
    }
}
