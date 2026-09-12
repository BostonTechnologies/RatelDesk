# Roles and tenant-scoped access

RatelDesk resolves access on the API from an application principal, its durable
role assignments, and the target organization. A role or organization claim
sent by a browser is not trusted as an authorization decision.

## Built-in roles

| Role | Scope | Access |
| --- | --- | --- |
| Instance Administrator | Instance | Full instance administration, including roles, tenant administration, and local accounts. It remains compatible with the `HelpdeskAdmin` role. |
| Tenant Administrator | Each assigned tenant | Tenant user, role-assignment, and settings permissions for that tenant. It has no database, identity-provider, or instance-administrator access. |
| Technician | Each assigned tenant | The standard staff incident, request, change, and self-service permission bundle. |
| Self-service User | Own resources in each assigned tenant | The self-service, incident-user, and request-user permission bundle. |

Built-in roles are protected. Instance administrators can create tenant-owned
custom roles in **Administration → Roles & Permissions**. A custom role can
only contain the published assignable permissions, and writer permissions must
also include their corresponding user/read permission. A tenant administrator
can use **Tenant roles** to create, edit, or delete custom roles owned by an
assigned tenant only. Its delegation ceiling is deliberately smaller: self
service and the incident, request, and change reader/writer permissions. It
cannot delegate tenant-management, data-management, or instance permissions.

## Tenant membership delegation

The **Tenant members** navigation item appears only when the API resolves at
least one tenant where the signed-in principal has `Tenant.Roles.Assign`. The
page lists local accounts in those tenants, can add or remove the
`SelfServiceUser` assignment or a safe custom role owned by that tenant, and
can invite a new local account directly into the selected tenant. The invitation
response contains a one-time activation token; the administrator must share it
through an approved secure channel.

This workflow deliberately cannot alter an existing global account, reset
credentials, enable or disable accounts, grant instance access, expose
non-delegable role assignments, or replace memberships in another tenant. A
new invitation always creates a non-administrator local account with only the
self-service assignment in the selected tenant. A pre-existing technician or
tenant-administrator assignment is preserved when delegated access changes.
Instance administrators use the full Team administration flow for all broader
account actions.

The API remains authoritative for the page and all direct requests:

- `GET /api/v1/tenant-admin/organizations` returns only tenants where the
  caller can assign tenant roles (or all enabled tenants to an instance
  administrator).
- Tenant member and membership routes require the same tenant-scoped
  permission and reject users outside that tenant and instance administrators.
- The membership route accepts `SelfServiceUser` and custom tenant roles only
  when their owner matches the selected tenant and their permissions remain
  within the delegation ceiling; it preserves every non-delegable assignment.

Membership changes increment the target local account's authorization revision
and security stamp. Its next API request is rejected until it signs in again,
so revoked tenant access does not persist in an existing API session.
Invitation and membership-change audit records contain the actor, target, and
tenant-scoped action, but never the activation token or a credential.

## External identities

OIDC identities and local accounts use the same resolved role and scope model.
External identity links are based on verified provider identity evidence; email
is profile data and is not an authorization-link key. The Web client may use
the effective-access projection to decide which navigation to display, but the
API re-resolves access for every protected operation.
