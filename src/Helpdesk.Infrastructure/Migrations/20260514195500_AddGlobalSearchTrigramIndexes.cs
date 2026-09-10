using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_tickets_title_trgm ON ""Tickets"" USING gin (""Title"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_tickets_description_trgm ON ""Tickets"" USING gin (""Description"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_tickets_trackingid_trgm ON ""Tickets"" USING gin (""TrackingId"" gin_trgm_ops);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_services_name_trgm ON ""Services"" USING gin (""Name"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_services_description_trgm ON ""Services"" USING gin (""Description"" gin_trgm_ops);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_requestforms_title_trgm ON ""RequestForms"" USING gin (""Title"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_requestforms_description_trgm ON ""RequestForms"" USING gin (""Description"" gin_trgm_ops);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_users_name_trgm ON ""Users"" USING gin (""Name"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_users_email_trgm ON ""Users"" USING gin (""Email"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_users_role_trgm ON ""Users"" USING gin (""Role"" gin_trgm_ops);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_customers_name_trgm ON ""Customers"" USING gin (""Name"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_customers_email_trgm ON ""Customers"" USING gin (""Email"" gin_trgm_ops);");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_organizations_name_trgm ON ""Organizations"" USING gin (""Name"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_organizations_dnsname_trgm ON ""Organizations"" USING gin (""DnsName"" gin_trgm_ops);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_organizations_contactinfo_trgm ON ""Organizations"" USING gin (""ContactInfo"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_organizations_contactinfo_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_organizations_dnsname_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_organizations_name_trgm;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_customers_email_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_customers_name_trgm;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_users_role_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_users_email_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_users_name_trgm;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_requestforms_description_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_requestforms_title_trgm;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_services_description_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_services_name_trgm;");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_tickets_trackingid_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_tickets_description_trgm;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_tickets_title_trgm;");
        }
    }
}
