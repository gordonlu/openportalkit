import { createServer } from "node:http";

const port = Number.parseInt(process.env.PORT || "3199", 10);
const failPublicRequests = process.env.OPK_MOCK_FAILURE === "1";
const publishedAt = "2026-07-14T08:00:00Z";
const responses = new Map([
  ["/api/public/datasets", [{
    code: "operations_benchmark",
    name: "Operations benchmark",
    description: "A traceable public benchmark dataset.",
    updatedAt: publishedAt,
  }]],
  ["/api/public/content", {
    items: [{
      id: "11111111-1111-1111-1111-111111111111",
      contentType: "Report",
      title: "Live manufacturing release",
      slug: "live-manufacturing-release",
      summary: "A live public API record.",
      canonicalUrl: "http://internal-api/content/live-manufacturing-release",
      markdownSnapshot: "http://internal-api/content/live-manufacturing-release.md",
      jsonSnapshot: "http://internal-api/api/public/content/live-manufacturing-release.json",
      publishedAt,
      updatedAt: publishedAt,
      visibilityPolicy: "Public",
    }],
    offset: 0,
    limit: 20,
    hasMore: false,
  }],
  ["/api/public/pages", {
    items: [{
      id: "22222222-2222-2222-2222-222222222222",
      title: "Live governance page",
      slug: "live-governance-page",
      summary: "A live public page.",
      canonicalUrl: "http://internal-api/pages/live-governance-page",
      markdownSnapshot: "http://internal-api/pages/live-governance-page.md",
      jsonSnapshot: "http://internal-api/api/public/pages/live-governance-page.json",
      publishedAt: "2026-07-13T08:00:00Z",
      updatedAt: publishedAt,
      revision: 3,
    }],
    offset: 0,
    limit: 20,
    hasMore: false,
  }],
]);

const server = createServer((request, response) => {
  const url = new URL(request.url || "/", `http://127.0.0.1:${port}`);
  if (url.pathname === "/health") {
    response.writeHead(200, { "Content-Type": "text/plain" });
    response.end("ok");
    return;
  }
  if (failPublicRequests) {
    response.writeHead(503, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ error: "dependency-unavailable" }));
    return;
  }
  if (url.pathname === "/api/public/search") {
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({
      items: [{
        targetType: "DataSet",
        targetId: "operations_benchmark",
        title: "Operations benchmark",
        summary: "A traceable public benchmark dataset.",
        url: "/api/public/datasets/operations_benchmark",
        score: 12,
        matchedFields: ["title"],
      }],
      offset: 0,
      limit: 20,
      hasMore: false,
    }));
    return;
  }
  const body = responses.get(url.pathname);
  if (!body) {
    response.writeHead(404, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ error: "not-found" }));
    return;
  }
  response.writeHead(200, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
});

server.listen(port, "127.0.0.1");
for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
