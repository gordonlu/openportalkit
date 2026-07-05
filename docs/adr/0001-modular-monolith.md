# ADR 0001: Modular Monolith First

## Status

Accepted.

## Context

OpenPortalKit needs clear module boundaries for a broad publishing framework, but early microservices would add operational complexity before the domain contracts are stable.

## Decision

Start with a modular monolith:

- one .NET solution
- separate host projects
- separate module projects
- shared kernel primitives
- event and outbox patterns for reliable cross-module work

## Consequences

- Feature work starts faster.
- Boundaries are visible in projects and docs.
- Host projects can compose modules.
- Later service extraction remains possible where a boundary proves stable and operationally justified.
