# OpenPortalKit 1.0 Release Candidate Checklist

Record the release commit, artifact checksums, operator, UTC timestamps, target topology, and evidence links with
every rehearsal. A checkbox without evidence is not a pass.

## Repository Gates

- [ ] `dotnet build OpenPortalKit.sln -m:1` has zero warnings and errors.
- [ ] `./tools/run-tests.ps1` passes, including PostgreSQL when `OPK_POSTGRES_INTEGRATION` is configured.
- [ ] `./tools/opk check-boundaries` passes.
- [ ] `./tools/opk check-agent-readiness` passes.
- [ ] vulnerable NuGet and npm package checks pass with no exception.
- [ ] `npm run lint`, `npm run build`, and `npm run test:e2e` pass in `apps/web`.
- [ ] all reference industry packs validate against manifest v1.

## Windows Server Rehearsal

- [ ] Publish hosts for `win-x64` from the release commit and record SHA-256 checksums.
- [ ] Run services under a non-administrator service account with write access only to required key/log paths.
- [ ] Configure exact `AllowedHosts`, HTTPS reverse proxying, forwarded-header trust, and firewall rules.
- [ ] Apply PostgreSQL migrations under the advisory lock after a verified backup.
- [ ] Verify liveness/readiness, graceful JobHost shutdown, structured trace IDs, and restart behavior.
- [ ] Confirm the Data Protection key ring is protected and its ACL excludes interactive users.
- [ ] Run live AgentSEO checks against the HTTPS release hostname.
- [ ] Restore the rehearsal backup to an isolated database and compare migration/checksum records.

## Authentication and Threat Review

- [ ] Register the exact OIDC redirect URI and use Authorization Code with PKCE.
- [ ] Confirm users without the configured role are denied and provider MFA is enforced.
- [ ] Confirm remote failure, sign-out, expired sessions, session-version revocation, and key rotation behavior.
- [ ] Exercise local break-glass lockout without revealing whether a username exists.
- [ ] Verify host-only Secure HttpOnly SameSite cookies and CSRF rejection through the production proxy.
- [ ] Review upload, migration CSV, open redirect, SSRF, XSS/CSP, rate-limit, secret, and audit boundaries.

## Promotion and Rollback

- [ ] Compare public HTML, Markdown, JSON, sitemap, RSS, search, and agent outputs with the approved candidate.
- [ ] Confirm no public-output-changing action bypasses audit/outbox handling.
- [ ] Record DNS/proxy rollback, database rollback constraints, and responsible operators.
- [ ] Promote traffic only after every environment-dependent item above has evidence.
