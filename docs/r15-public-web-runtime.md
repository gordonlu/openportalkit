# R15 Public Web Runtime

## Delivered Batch

R15 Batch 1 connects all five public profiles to the same published content, page, dataset, and search contracts:

- `GET /api/public/content?offset=0&limit=20`
- `GET /api/public/pages?offset=0&limit=20`
- `GET /api/public/datasets`
- `GET /api/public/search?q=...&offset=0&limit=20`

The page-list endpoint applies publication status and `published_at` visibility before stable pagination. Its summary
contract omits page blocks and actor identifiers. The dataset catalogue reads durable PostgreSQL stores and exposes
only public datasets. `0018_structured_data.sql` preserves dataset visibility plus schema and record provenance:
source, generated import batch, as-of date, schema version, checksum, and timestamps.

The Web client is server-only. It validates list envelopes and required public fields, limits each request to five
seconds, requests at most 20 items, and revalidates after 60 seconds. Profile routes use request-time server rendering
so deployment environment changes cannot leave a statically generated demo page in a live process. Canonical paths returned by ApiHost must match
the expected `/content/` or `/pages/` boundary before links are rebuilt against `OPK_PUBLIC_BASE_URL`.

Interactive search calls a same-origin Next.js route. That route validates the query, calls ApiHost server-to-server,
and accepts only content, page, or public-dataset result paths before rebuilding links against the public origin.
ApiHost builds the read-only index from current published stores, so a restart is not required after publication.
The implementation deliberately caps each indexed content family at 1,000 documents; an external search adapter for
larger deployments is a post-1.0 concern.

## Runtime Modes

`demo` is the default so framework examples and visual regression tests remain deterministic. It reads only the
versioned fixtures in `apps/web/src/lib/example-sites.ts`.

`live` requires:

```text
OPK_WEB_DATA_MODE=live
OPK_API_BASE_URL=http://127.0.0.1:5051
OPK_PUBLIC_BASE_URL=https://portal.example.com
```

`OPK_API_BASE_URL` is used only by the Next.js server. `OPK_PUBLIC_BASE_URL` is used for browser-visible content,
page, sitemap, RSS, LLMs.txt, and OpenAPI links. URLs with credentials, non-HTTP schemes, or fragments are rejected.

ApiHost errors, malformed responses, and timeouts produce an explicit unavailable state. Live mode never falls back
to fixtures, because presenting example records as current public information would be a publication integrity bug.

## Boundary and Security Impact

- The Web runtime calls public GET endpoints only and carries no administrator cookie or token.
- Drafts and future publications remain filtered in Content stores, not in browser code.
- Page blocks, creator IDs, and updater IDs are absent from page-list responses.
- Private datasets are filtered in the Data query service and never reach Web responses or public search.
- The client accepts only expected canonical path families before moving them to the public origin.
- No industry entity was introduced into core, and all profiles share the same public contract.
- This slice does not change public output, so it creates no new public-output-changing action or audit path.

## Verification

Run default visual and interaction coverage separately from the live contract harness:

```bash
npm run lint
npm run build
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e:live
```

The live harness runs normal and dependency-failure configurations sequentially on desktop and mobile. It verifies
content, pages, datasets, search, canonical metadata, public-origin links, the absence of fixture records, and
explicit 503 behavior.
Backend coverage includes Content/Data unit tests, AgentAccess OpenAPI tests, Host integration tests, and PostgreSQL
visibility, ordering, pagination, schema, and record-provenance integration.

## Batch Status

Batch 1 is complete. External search providers, distributed cache invalidation, and high-volume catalogue pagination
are possible 1.1 adapters and do not block the 1.0 release candidate.
