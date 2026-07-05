# Dashboard

Dashboard is a core capability. It helps operators understand whether a portal is healthy, fresh, searchable, agent-readable, and operationally reliable.

## Principle

Dashboard aggregates signals from Content, Data, Workflow, Jobs, Audit, Search, AgentAccess, and infrastructure. It must not own business state.

## Initial Areas

- Site operations
- Content freshness and workflow queues
- Structured data freshness and import quality
- Search health
- Job health
- AgentSEO readiness
- Audit activity

## Pack Extensions

Industry-specific dashboard cards belong inside the relevant industry pack and should consume generic dashboard extension contracts.
