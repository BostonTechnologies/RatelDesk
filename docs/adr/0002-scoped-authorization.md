# ADR 0002: explicit principal role assignments with scoped grants

Status: accepted

RatelDesk authorization is based on a stable application principal and scoped
role assignments, not on a free-text `User.Role`, an identity-provider tenant
claim, or an email match. The durable assignment shape is:

`(ApplicationPrincipalId, RoleId, ScopeKind, OrganizationId)`

Instance administration is an explicit instance-scoped built-in role.
Operational grants are organization-scoped. Self-service grants are restricted
to the linked contact's own resources within that organization. A request's
active tenant only narrows an already-granted scope; it never creates one.
Missing or invalid application scope fails closed.

External identities are resolved only through an explicit verified issuer and
subject (or configured provider identifier) link. Email remains profile data and
may assist an administrator in an invitation workflow, but is never a login or
authorization-link key. Existing valid external links are retained during
adoption.

The API is authoritative: policies and resource checks evaluate both permission
and target organization together. A global union of permissions and a separate
union of organizations is not valid. The Web client receives an effective-access
projection for navigation only and cannot make an authorization decision.

