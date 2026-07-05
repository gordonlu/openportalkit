# Architecture

OpenPortalKit starts as a modular monolith. The goal is clear code boundaries without premature microservices.

## Runtime Shape

- `OpenPortalKit.AdminHost` for administrative UI
- `OpenPortalKit.ApiHost` for public and integration APIs
- `OpenPortalKit.JobHost` for background processing
- `OpenPortalKit.AppHost` for local orchestration placeholder
- `apps/web` for the future public Next.js rendering layer

## Core Principles

- Core models are industry-neutral.
- Publishing events are first-class.
- Public resources should support human-readable and machine-readable outputs.
- Dashboard aggregates signals; it does not own business state.
- Destructive changes should use archive, versioning, dry runs, or audit trails.

## Event Pipeline

Publishing and data events should flow through an outbox-backed pipeline:

```txt
write transaction
-> outbox message
-> background handler
-> search/sitemap/RSS/snapshot/cache/dashboard updates
-> audit and processing result
```

## Storage Direction

PostgreSQL is the primary relational store. SQLite may be used for local lightweight development and tests. Redis is planned for caching, queues, and coordination where useful.

## Frontend Direction

The public site should use Next.js App Router, TypeScript, server rendering, static rendering, ISR, semantic HTML, and accessible forms and links.

The initial admin surface should stay reliable and server-first with Razor Pages/MVC, with HTMX or Blazor SSR considered later.
