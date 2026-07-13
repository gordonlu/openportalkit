# OpenPortalKit 1.x Compatibility Policy

## Stable Contracts

OpenPortalKit 1.x treats the following surfaces as stable after the 1.0 release candidate is approved:

- public HTTP paths documented by `/api/openapi.json`;
- public response shapes and `X-OpenPortalKit-Contract-Version: 1.0.0`;
- industry pack manifest `manifestVersion: 1.0` and its published JSON Schema;
- `opk` command names, exit codes, required options, and JSON check rule identifiers;
- numbered PostgreSQL migrations after they have shipped;
- example fixture schema `opk.example.v1`.

Backward-compatible fields, endpoints, check results, and optional manifest capabilities may be added in a minor
release. Existing required fields, meanings, operation IDs, and successful workflows are not removed or changed
within 1.x. Security fixes may reject previously accepted unsafe input without a major version change.

## .NET Extension Surface

The module descriptors, interfaces explicitly referenced by `MODULES.md`, industry pack contracts, and public
output contracts are supported extension points. Other public C# types are implementation-visible, not a promise
of binary compatibility. They may change in a minor release while the stable HTTP, CLI, manifest, migration, and
documented module contracts remain compatible.

Consumers should integrate through documented module interfaces rather than concrete in-memory/PostgreSQL stores,
host startup types, Razor Page models, or sample-data builders. A future type promoted to the stable SDK surface
must be documented here and receive compatibility tests first.

## Upgrade Rules

- Never edit an applied migration; add the next numbered migration.
- Validate all installed packs with the target CLI before upgrading a host.
- Back up PostgreSQL and verify the checksum before applying migrations.
- Run boundary, AgentSEO, dependency, Host, PostgreSQL, and Web E2E gates before traffic promotion.
- Treat a major public contract or pack manifest version as an explicit migration project, not an in-place edit.
