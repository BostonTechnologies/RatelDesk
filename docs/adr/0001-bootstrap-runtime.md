# ADR 0001: bootstrap before the application runtime

Status: accepted

RatelDesk starts in one of four durable bootstrap states: `Unconfigured`,
`Configuring`, `Ready`, or `RecoveryRequired`. The state is held in an API-only
mounted bootstrap volume; it is not inferred from the presence of a database or
a user row.

Before `Ready`, the API exposes only liveness, safe branding defaults, setup
status, and setup operations. It does not resolve database-backed branding,
Identity stores, Hangfire, or optional integrations. A bootstrap descriptor
contains the selected provider, protected connection secret reference, durable
operation ID, and transition journal. It never stores an administrator password.

The setup service prepares the selected provider and schema before it accepts
the first administrator password. Completion creates the initialization marker,
organization, Identity principal, protected instance-administrator assignment,
and role catalog in the application database transaction. The descriptor is
then activated using the same operation ID. A restart reconciles those two
boundaries; it neither duplicates the principal nor reopens setup.

The normal runtime is composed once, after a persisted state transition. It
must not mutate the built DI container or select a provider per request. A
failed or missing selected database after prior initialization is
`RecoveryRequired`, never a reason to create a new SQLite database.

Configuration precedence is deployment-owned configuration, durable
installer/admin configuration, then product defaults. Example values shipped in
application configuration are not deployment-owned configuration.

