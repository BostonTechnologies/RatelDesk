using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.SqliteMigrations.Migrations.Helpdesk;

[DbContext(typeof(HelpdeskDbContext))]
[Migration("20260912142100_AddInitializationTimeZone")]
public partial class AddInitializationTimeZone : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeZoneId",
            table: "InstanceInitializations",
            type: "TEXT",
            maxLength: 128,
            nullable: false,
            defaultValue: "UTC");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TimeZoneId",
            table: "InstanceInitializations");
    }
}
