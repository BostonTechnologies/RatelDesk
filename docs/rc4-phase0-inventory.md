# rc.4 Phase 0 inventory

Starting point: `cee9e1cba4deaeb1795f63337a7ceb73ffcdf66f` on `origin/main`.
The repository targets .NET 10 through `global.json`; the release line is
owned by `Directory.Build.props`.

| Surface | Existing state | rc.4 direction |
| --- | --- | --- |
| API startup | Requires `HelpdeskDb` and PostgreSQL Hangfire before serving. | Split bootstrap from the selected application runtime and gate workers until ready. |
| Database | Npgsql/pgvector is the runtime default; SQLite package is present but query and migration assumptions remain. | Explicit provider selection, provider migrations, SQLite query compatibility, and declared semantic-search capability. |
| Authentication | API validates external JWTs; Web uses OIDC/cookie forwarding; legacy BCrypt/JWT login is Development-only. | API-owned ASP.NET Core Identity local accounts, with OIDC retained as an opt-in scheme. |
| Identity links | Customer links could fall back to email and provisioning accepted body-controlled identity fields. | Use only validated issuer/subject or provider ID and explicit links. |
| Roles | Free-text user role plus claim bundles; generic role CRUD. | Protected built-ins, custom roles, permissions, and scoped assignments. |
| Tenant boundary | `Organization.Id` is the application tenant, but `tid` could be treated as a tenant. | Resolve application organization only from an explicit application relationship. |
| Generic CRUD | Roles, organizations, customers, KB, assets, and automation had only authentication gating. | Restrict immediately; replace incrementally with scoped, operation-specific handlers. |
| Streams and jobs | SignalR, worker services, and Hangfire begin with the normal host. | Reauthorize subscriptions and prevent workers from starting before ready. |

The initial route matrix is maintained from API endpoint groups: ticket,
incident, request, change, worklog, attachment, self-service, user, tenant,
role, knowledge, asset, automation, notification, timeline, reporting,
orchestration, MCP/machine identity, and generic CRUD surfaces. Each rc.4
handler must declare read/write/delete/approve/export/automation permission and
validate source and destination organization scope before pagination or mutation.
