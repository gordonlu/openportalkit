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

## Manifest Contract

`pack.json` uses manifest contract `1.0`, documented by
`schemas/industry-pack-manifest.v1.schema.json`. The manifest must declare:

- `manifestVersion`: exactly `1.0` for the current loader;
- `version`: the pack's three-part semantic version;
- `requiresCore`: the minimum compatible OpenPortalKit core version;
- every registration switch and every resource list, including empty lists;
- relative `.json` resource paths contained by the pack directory.

Unknown or missing properties, unsupported manifest versions, path traversal, duplicate resources, invalid JSON,
registration/resource mismatches, and newer core requirements fail closed. Every loaded resource receives a SHA-256
checksum, and enabled installations reject checksum drift during startup rehydration.

Validate one pack or the complete portfolio after building:

```bash
./tools/opk industry-pack validate --path industry-packs/Technology
./tools/opk industry-pack validate --path industry-packs --format json
```

Changing a resource or manifest requires a pack version increment. Backward-compatible additions increment the minor
version while fixes increment the patch version. Breaking resource semantics require a major version increment and a
documented migration. A future manifest format uses a new `manifestVersion`; existing loaders must reject it until
support is implemented.

## Pack Boundaries

The Finance Pack is a demonstration of regulated, data-heavy publishing. Its concepts must remain inside `industry-packs/Finance` until a generic core abstraction is explicitly justified and documented.

Technology Pack must not turn core into product lifecycle management, issue tracking, or a developer platform.

Education Pack must not turn core into an LMS, student information system, assessment engine, or enrollment transaction system.

Entertainment Pack must not turn core into a streaming service, ticketing platform, royalty system, or digital-rights-management system.

Core must run without any industry pack.
