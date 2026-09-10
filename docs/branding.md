# Instance branding

RatelDesk is the immutable upstream product identity. A deployment can present its own customer-facing identity without changing namespaces, assemblies, package IDs, database schema names, routes, OIDC identifiers, cookies, telemetry resource names, or the `DataProtection:ApplicationName` cryptographic isolation value.

## Defaults and precedence

Without configuration, the application displays **RatelDesk**, uses the bundled mark and wordmark, and uses the RatelDesk email sender fallback. For each field the effective value is resolved as:

1. `Branding__*` deployment configuration;
2. Administrator value in **Administration → Branding & Identity**;
3. the built-in RatelDesk default.

Deployment-managed values are visible in the Admin page and labelled as environment-managed. They cannot be changed there. Tenant email branding remains supported and is applied above the effective global brand for tenant-specific name, logo, footer, colour, sender, and reply-to settings.

## Environment variables

Use the names in [`.env.example`](../.env.example): `Branding__ApplicationName`, `Branding__OrganizationName`, `Branding__ApplicationUrl`, `Branding__OrganizationUrl`, `Branding__SupportUrl`, `Branding__SupportEmail`, `Branding__LogoUrl`, `Branding__CompactLogoUrl`, `Branding__FaviconUrl`, `Branding__EmailFromDisplayName`, and `Branding__Tagline`.

For Compose, make these values available to the API service. For example:

```yaml
environment:
  Branding__ApplicationName: Acme Service Desk
  Branding__ApplicationUrl: https://support.example.com
  Branding__EmailFromDisplayName: Acme Service Desk
```

`ApplicationUrl` is the canonical base for generated application links. Use an HTTPS public URL in production; it must be an absolute HTTP(S) URL. Host logos and favicons on a publicly reachable HTTPS endpoint, or retain the bundled `/branding/` artwork.

The bundled browser defaults are `/branding/rateldesk-mark.webp`, `/branding/rateldesk-wordmark.webp`, and `/branding/rateldesk-splash.webp`; PNG counterparts are included for transparent-image fallback. The email default is the mail-client-compatible `/email-brand/rateldesk-email-wordmark.png`. `LogoUrl`, `CompactLogoUrl`, and `FaviconUrl` continue to override these upstream defaults, and tenant branding remains more specific where configured.

## Email templates

Existing `BRAND_NAME`, `LOGO_HTML`, and `FOOTER_HTML` tokens continue to work. Templates may also use structured, escaped tokens such as `{{brand.application_name}}`, `{{brand.organization_name}}`, `{{brand.application_url}}`, `{{brand.organization_url}}`, `{{brand.support_url}}`, `{{brand.support_email}}`, `{{brand.logo_url}}`, `{{brand.email_from_display_name}}`, and `{{brand.tagline}}`.

An explicitly configured tenant sender name remains more specific than the global email-display-name fallback.

## Upstream attribution

Repository, licence, and open-source attribution remain RatelDesk and `BostonTechnologies/RatelDesk`. Branding only changes human-facing deployment identity; it never changes security or protocol compatibility identities.
