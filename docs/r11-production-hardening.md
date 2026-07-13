# R11 Production Hardening and Migration Toolkit

R11 makes OpenPortalKit operable as a serious self-hosted service before adding legacy migration workflows. Work is delivered in reviewable batches and acceptance criteria are not marked complete until their real dependencies are exercised.

## Batch 1: HTTP Production Baseline

`OpenPortalKit.Infrastructure.Production` is a host-neutral ASP.NET Core infrastructure project shared by AdminHost and ApiHost. It owns:

- validated, bounded `X-Trace-Id` propagation and structured logging scopes
- Content Security Policy, clickjacking, MIME-sniffing, referrer, opener, and browser-permission headers
- forwarded-header handling with the framework's restricted proxy trust defaults
- configurable HTTPS redirection and production HSTS
- per-client fixed-window limits with bounded queues and Problem Details rejection bodies
- separate liveness and readiness endpoints
- startup validation for invalid request-limit configuration

Admin traffic receives a stricter default request budget than public API traffic. Health endpoints remain subject to the global limiter to prevent probe abuse, while their handlers are anonymous in preparation for the authentication batch.

## Verified Behavior

The package-free infrastructure tests cover security-header completeness, safe caller trace IDs, rejection of injection-shaped trace IDs, and defensive production defaults. A real ApiHost run verified both health endpoints, response headers, safe trace propagation, and random W3C replacement IDs.

## Remaining R11 Work

The next security batch must complete permission policies, antiforgery coverage auditing, CSP nonces, upload validation, and secret-provider configuration. Reliability then adds real PostgreSQL/Redis/search/storage readiness probes, migration locking and drift detection, graceful shutdown evidence, backup/restore procedures, and rebuild tooling. Performance and the migration toolkit follow those foundations.

## Batch 2: Administrator Authentication

AdminHost now defaults to a fallback authorization policy that requires an authenticated `Administrator` role for every route. Login and health endpoints are explicit anonymous exceptions, static login assets remain available, and logout is POST-only with Razor antiforgery validation.

Credentials use a versioned salted PBKDF2-SHA512 format with 210,000 iterations and fixed-time comparison. Generate a deployment hash with `tools/new-admin-password-hash.ps1` and provide it through the `OpenPortalKit__AdminAuthentication__PasswordHash` secret or environment variable. Plaintext passwords do not belong in JSON configuration.

The authentication cookie is host-only, HTTP-only, `SameSite=Strict`, secure-only, non-persistent, and uses a sliding idle timeout bounded by an absolute session lifetime. Production startup fails closed when authentication is required but no supported password hash is configured. Development explicitly opts out in `appsettings.Development.json`; teams may override that setting to exercise authentication locally.

AdminHost uses a high-entropy per-request CSP nonce and does not permit arbitrary inline scripts. The remaining inline-style allowance is isolated from script execution and will be removed as component styling moves fully into static stylesheets. The non-Razor analytics POST performs explicit antiforgery validation in addition to authenticated-cookie and same-site controls.

### Brute-Force and Cookie Controls

The login endpoint has a dedicated per-source five-minute request budget in addition to the global AdminHost limit. Local authentication locks a source after the configured consecutive-failure threshold, returns the same response for unknown users, wrong passwords, and locked sources, clears failures only after success, and audits success, failure, and lockout without storing passwords. Local lockout state is process-local and is intended for a single-instance or emergency account; clustered production deployments should use OIDC and enforce distributed risk controls at the identity provider and edge gateway.

Every cookie carries an absolute start time and configurable session version. Changing `SessionVersion` revokes all existing local and OIDC sessions after restart. A custom shared Data Protection key ring is accepted only with an X.509 key-encryption certificate. On a single Windows Server, the framework's default user-profile key ring uses Windows protection; multi-instance deployments must configure a protected shared key repository. See Microsoft's [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0) and [key storage guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0).

### Single Sign-On

Set `OpenPortalKit:AdminAuthentication:Mode` to `OpenIdConnect` to use an enterprise identity provider. AdminHost requires an HTTPS authority, client ID, client secret, Authorization Code flow with PKCE, and the configured `RequiredRole` claim. Tokens are not stored in the authentication cookie. Missing roles and remote failures are denied and audited. `Local` remains available as a separately controlled break-glass mode.
