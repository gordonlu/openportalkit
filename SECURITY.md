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
