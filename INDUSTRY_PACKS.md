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

## Reference Packs

R10 includes four independently optional reference packs:

- `Finance`: regulated, data-heavy, high-trust publishing
- `Technology`: product, engineering, documentation, release, and developer-community publishing
- `Education`: program, course, faculty, research, admissions, and academic-calendar publishing
- `Entertainment`: title, release, event, editorial, talent, and media-catalog publishing

Every pack uses the same manifest and registration pipeline. A pack may not depend on another pack, and core hosts must start with no packs enabled.

## Pack Boundaries

The Finance Pack is a demonstration of regulated, data-heavy publishing. Its concepts must remain inside `industry-packs/Finance` until a generic core abstraction is explicitly justified and documented.

Technology Pack must not turn core into product lifecycle management, issue tracking, or a developer platform.

Education Pack must not turn core into an LMS, student information system, assessment engine, or enrollment transaction system.

Entertainment Pack must not turn core into a streaming service, ticketing platform, royalty system, or digital-rights-management system.

Core must run without any industry pack.
