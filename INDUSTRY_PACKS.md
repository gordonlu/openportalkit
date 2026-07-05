# Industry Packs

Industry packs adapt OpenPortalKit to vertical domains without polluting the core model.

## Pack Contents

An industry pack may contain:

- content type definitions
- dataset schemas
- page templates
- validation rules
- publishing rules
- dashboard card definitions
- seed data
- importer definitions
- public page components
- admin extensions
- terminology overrides
- disclaimer templates
- sample data

## Initial Strategy

Use manifests, JSON schemas, seed data, templates, rules, and dashboard card definitions first. Avoid a complex runtime plugin marketplace in early versions.

## Finance Pack Boundary

The Finance Pack is a demonstration of regulated, data-heavy publishing. Its concepts must remain inside `industry-packs/Finance` until a generic core abstraction is explicitly justified and documented.

Core must run without the Finance Pack.
