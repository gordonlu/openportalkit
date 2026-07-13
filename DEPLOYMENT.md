# Deployment

OpenPortalKit is being hardened for self-hosted production operation. Container and Windows Server packaging remain in progress; the HTTP production baseline below is implemented by both web hosts.

## Planned Local Services

- PostgreSQL
- Redis
- API host
- Admin host
- Job host
- Public web app

## Production Requirements

- Liveness at `/health/live` and readiness at `/health/ready`
- Structured log scopes and responses with validated `X-Trace-Id` values
- HTTPS redirection and HSTS outside Development
- Browser security headers and per-client fixed-window request limits
- OpenTelemetry export
- Secret management
- Database migration safety
- Backup and restore documentation
- Cache invalidation strategy
- Search index rebuild procedure

## Reverse Proxy

Terminate TLS at IIS, nginx, or another managed proxy and forward `X-Forwarded-For` and `X-Forwarded-Proto`. The application trusts the ASP.NET Core loopback proxy defaults. Explicitly configure known proxy addresses or networks when the proxy is not local; never trust forwarded headers from arbitrary clients.

Orchestrators should use `/health/live` only to decide whether the process must restart and `/health/ready` to decide whether it can receive traffic. `/health` remains a temporary redirect to readiness for compatibility.

## Production Configuration

`OpenPortalKit:Production` controls HTTPS redirection, HSTS, rate limiting, request budgets, and queue size. Defaults are defensive. Development configuration disables only HTTPS redirection so local HTTP launch profiles remain usable.

The first R11 batch includes a self readiness probe. Database, Redis, search, object-storage, authentication, CSP nonce, and migration-state probes must be completed before a production release is declared ready.

## Administrator Credential

Generate the initial administrator password hash using hidden PowerShell input:

```powershell
./tools/new-admin-password-hash.ps1
```

Store the resulting value as the `OpenPortalKit__AdminAuthentication__PasswordHash` environment secret. Set the username separately with `OpenPortalKit__AdminAuthentication__UserName`. AdminHost production startup fails when authentication is required and the hash is absent. Rotate the credential by replacing the secret and restarting AdminHost; existing cookies remain bounded by their configured idle and absolute lifetimes.

For enterprise SSO, set `OpenPortalKit__AdminAuthentication__Mode=OpenIdConnect` and provide `Authority`, `ClientId`, `ClientSecret`, and `RequiredRole` through deployment configuration and secrets. The authority must use HTTPS. Prefer this mode for clustered production and keep the local credential as a controlled break-glass account only when operational policy requires it.

Changing `OpenPortalKit__AdminAuthentication__SessionVersion` and restarting AdminHost invalidates all existing cookies. For a web farm, configure `DataProtectionKeyPath` plus an X.509 certificate path/password and restrict the key directory ACL to the service account. Never mount an unencrypted shared key ring.
