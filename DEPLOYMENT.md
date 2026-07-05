# Deployment

Deployment is not implemented yet. This document records the intended direction so early code does not block production operation later.

## Planned Local Services

- PostgreSQL
- Redis
- API host
- Admin host
- Job host
- Public web app

## Production Requirements

- Health and readiness checks
- Structured logs with trace IDs
- OpenTelemetry export
- Secret management
- Database migration safety
- Backup and restore documentation
- Cache invalidation strategy
- Search index rebuild procedure
