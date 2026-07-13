# Security

OpenPortalKit is intended for enterprise publishing, so security is a product requirement.

## Baseline Requirements

- Server-side permission checks for admin actions
- CSRF protection for state-changing admin requests
- XSS protection and content sanitization
- File upload validation with MIME and size checks
- Secure password handling
- Rate limiting for sensitive endpoints
- Admin session timeout
- Security headers
- Secret management through environment or platform facilities
- Audit logs for public-output-changing actions

## Reporting

Do not publish exploit details in public issues before maintainers have had time to assess the report. Use the repository's private security reporting channel once one is configured.

## Dependency Policy

Dependencies should be justified by clear operational value. Prefer built-in .NET and ASP.NET Core capabilities until a module needs a dedicated provider package.

CI scans direct and transitive .NET packages against NuGet advisories and runs npm audit at the moderate threshold. A finding fails the build. Security overrides must stay narrowly scoped, retain a reproducible lockfile, and pass lint plus a production frontend build.

## Production Configuration

- Set an explicit semicolon-delimited `AllowedHosts` value for AdminHost and ApiHost. Production startup rejects `*`.
- Supply passwords, OIDC client secrets, certificate passwords, and connection strings through environment variables or the Windows service secret facility, not committed JSON files.
- Set `OpenPortalKit:AgentAccess:OutputGeneration:PublicBaseUrl` to the externally reachable HTTPS origin. AdminHost and JobHost reject HTTP in Production.
- Use a protected shared Data Protection key ring and X.509 key encryption for multiple AdminHost instances.
- Prefer OIDC with provider-side MFA and distributed lockout controls. Keep local authentication as a controlled break-glass path.

## Upload Boundary

`AssetUploadValidator` is the mandatory pre-storage gate for future Asset upload endpoints. It validates a bounded size, plain filename, allow-listed extension, declared MIME, and file signature. SVG is intentionally rejected because it can contain active content. Storage adapters must generate their own opaque key and must not use the client filename as a path.

The current migration CSV endpoint applies its own stricter 5 MB UTF-8 CSV policy and never stores the uploaded source file. No general Asset upload endpoint is exposed yet.
