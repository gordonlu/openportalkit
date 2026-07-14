# OpenPortalKit Web

`apps/web` is the public Next.js runtime for the five project profiles. It supports an explicit fixture-backed demo
mode and a server-rendered live mode backed by ApiHost's read-safe public contracts.

## Current Boundary

`OPK_WEB_DATA_MODE=demo` is the default and reads versioned fixtures from `src/lib/example-sites.ts`. Never present
those counters or publications as production CMS data.

`OPK_WEB_DATA_MODE=live` reads published content and portal pages from ApiHost on the server. It does not send admin
credentials, request drafts, or silently fall back to fixtures when ApiHost is unavailable. It also renders the
public dataset catalogue, proxies interactive public search through a same-origin read-only route, and emits
canonical/OpenGraph metadata from the configured public origin.

Profile routes are rendered by the Next.js server on each request so deployment-time environment changes are not
frozen into a static demo build. Validated ApiHost list responses retain their explicit 60-second server cache.

## Live Configuration

```bash
OPK_WEB_DATA_MODE=live \
OPK_API_BASE_URL=http://127.0.0.1:5051 \
OPK_PUBLIC_BASE_URL=https://portal.example.com \
npm run dev
```

- `OPK_API_BASE_URL` is the server-side ApiHost origin. It can use an internal network address.
- `OPK_PUBLIC_BASE_URL` is the browser-visible HTTPS origin used to rebuild canonical public links.
- Both values must be HTTP(S) URLs without embedded credentials or fragments.
- Public list responses are schema-checked, limited to 20 items per resource, cached for 60 seconds, and bounded by a
  five-second request timeout.

## Source-Level Customization

- `src/lib/branding.json`: validated site identity, approved assets, color tokens, typography preset, navigation, and footer.
- `src/lib/project-profile.json`: generated project name, selected profile, and selected industry packs.
- `public/`: customer-owned public images, fonts, icons, and downloadable static assets.
- `src/app/globals.css`: public-site design tokens, responsive rules, and typography.
- `src/components/example-portal.tsx`: profile page composition.
- `src/lib/example-sites.ts`: visual profiles and explicit demo-only publication fixtures.
- `src/lib/public-api.ts`: server-only live public API/search client and response validation.

AdminHost styling is separate under `src/OpenPortalKit.AdminHost/wwwroot/css/site.css`. Changing AdminHost CSS does
not brand the public Web application.

Source customization is supported. Runtime arbitrary CSS or script injection is intentionally not supported because
it would weaken CSP, review, and publishing boundaries.

Validate branding paths, formats, file sizes, actual dimensions, SVG safety, links, and contrast from the repository
root before building:

```bash
./tools/opk branding validate --root .
```

When `assets.logo` is `null`, the header uses `site.shortName`; a missing declared file is a validation failure, not a
silent network fallback. Full contract details are in `docs/r15-branding-assets.md`.

## Development and Verification

```bash
npm ci
npm run dev
npm run lint
npm run build
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e
PLAYWRIGHT_CHROME_PATH=/usr/bin/google-chrome npm run test:e2e:live
```

The current production command is `npm run start` after `npm run build`; it requires a supported Node.js runtime.
There is not yet a repository-owned Windows service wrapper or standalone Web artifact. Those are R15 delivery work.
