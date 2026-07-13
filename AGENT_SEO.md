# AgentSEO

AgentSEO means trustworthy, structured publishing for agents, RAG systems, search engines, and human readers. It is not ranking manipulation.

## Required Public Forms

Important public resources should eventually expose:

- HTML
- Markdown snapshots
- JSON snapshots
- RSS or Atom where feed semantics apply
- sitemap entries
- OpenAPI descriptions for public read endpoints

## Readiness Signals

Content readiness should consider title, description, canonical URL, structured data, summary, key facts, published date, updated date, source, snapshots, sitemap inclusion, RSS inclusion, semantic HTML, and accessible links/forms.

Dataset readiness should consider schema endpoint, JSON records endpoint, CSV export, source, as-of date, version, and checksum.

## Bot Policy

The platform should support configuration for search bots, training bots, allow lists, and crawl delay.

## Public Contract Version

Public JSON and CSV API responses expose `X-OpenPortalKit-Contract-Version`. The 1.0 contract value is `1.0.0` and
matches `info.version` in `/api/openapi.json`. Public HTML, Markdown, sitemap, RSS, robots, and discovery resources
remain representation contracts documented by OpenAPI and validated by `opk check-agent-readiness`.

Within the 1.x line, existing fields and operations are not removed or reinterpreted. Optional fields and new
operations may be added. A breaking change requires a new major contract version, migration guidance, and a parallel
deprecation period rather than silently changing an existing endpoint.
