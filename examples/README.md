# Runnable Example Portals

The five R12 examples share the production-oriented Next.js rendering shell in `apps/web`, while preserving
different information architectures for corporate news, structured data, research, activities, and investor
disclosures. They are statically generated and require no backend to review the UI.

```bash
cd apps/web
npm ci
npm run dev
```

Open `http://localhost:3000/examples/corporate` and use the **Examples** menu to switch portals. For a release
build, run `npm run build && npm run start`.

The links to OpenAPI, RSS, sitemap, and agent outputs intentionally target ApiHost paths. Run ApiHost behind the
same origin in a complete deployment. The standalone UI remains useful for layout and content-model review.

Each example contains `example.json`, a versioned fixture describing its route, modules, public outputs, and
optional industry pack. These fixtures are documentation and release evidence; they do not introduce domain
entities into core.
