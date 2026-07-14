import "server-only";

import type { ExamplePublication } from "./example-sites";

const PAGE_SIZE = 20;
const REQUEST_TIMEOUT_MS = 5_000;

type PublicListItem = {
  id: string;
  title: string;
  publishedAt: string;
  canonicalUrl: string;
  contentType?: string;
};

type PublicList = {
  items: PublicListItem[];
  hasMore: boolean;
};

export type PortalDataset = {
  code: string;
  name: string;
  description: string;
  updatedAt: string;
  href: string;
};

export type PortalSearchResult = {
  id: string;
  title: string;
  summary: string;
  type: string;
  href: string;
};

export type PortalRuntimeData = {
  mode: "demo" | "live";
  status: "ready" | "unavailable";
  publications: ExamplePublication[];
  datasets: PortalDataset[];
  statistics: { value: string; label: string }[];
  publicBaseUrl?: string;
};

export function getPortalDataMode(): "demo" | "live" {
  const mode = process.env.OPK_WEB_DATA_MODE?.trim().toLowerCase() || "demo";
  if (mode !== "demo" && mode !== "live") {
    throw new Error("OPK_WEB_DATA_MODE must be either 'demo' or 'live'.");
  }
  return mode;
}

export async function getPortalRuntimeData(): Promise<PortalRuntimeData> {
  if (getPortalDataMode() === "demo") {
    return { mode: "demo", status: "ready", publications: [], datasets: [], statistics: [] };
  }

  const apiBaseUrl = parseBaseUrl("OPK_API_BASE_URL", process.env.OPK_API_BASE_URL);
  const publicBaseUrl = parseBaseUrl(
    "OPK_PUBLIC_BASE_URL",
    process.env.OPK_PUBLIC_BASE_URL,
  );

  try {
    const [content, pages, datasets] = await Promise.all([
      fetchPublicList(apiBaseUrl, "/api/public/content"),
      fetchPublicList(apiBaseUrl, "/api/public/pages"),
      fetchPublicDatasets(apiBaseUrl, publicBaseUrl),
    ]);
    const publications = [
      ...content.items.map((item) => toPublication(item, publicBaseUrl, false)),
      ...pages.items.map((item) => toPublication(item, publicBaseUrl, true)),
    ].sort((left, right) => right.timestamp - left.timestamp);

    return {
      mode: "live",
      status: "ready",
      publications: publications.map((publication) => ({
        id: publication.id,
        title: publication.title,
        category: publication.category,
        date: publication.date,
        freshness: publication.freshness,
        format: publication.format,
        href: publication.href,
      })),
      datasets,
      statistics: [
        { value: formatCount(content.items.length, content.hasMore), label: "Published content loaded" },
        { value: formatCount(pages.items.length, pages.hasMore), label: "Public pages loaded" },
        { value: String(datasets.length), label: "Public datasets available" },
      ],
      publicBaseUrl: publicBaseUrl.toString().replace(/\/$/, ""),
    };
  } catch (error) {
    console.error("OpenPortalKit public API is unavailable.", error);
    return {
      mode: "live",
      status: "unavailable",
      publications: [],
      datasets: [],
      statistics: [
        { value: "--", label: "Published content loaded" },
        { value: "--", label: "Public pages loaded" },
        { value: "--", label: "Public datasets available" },
      ],
      publicBaseUrl: publicBaseUrl.toString().replace(/\/$/, ""),
    };
  }
}

export function getConfiguredPublicBaseUrl(): URL | undefined {
  if (getPortalDataMode() !== "live") return undefined;
  return parseBaseUrl("OPK_PUBLIC_BASE_URL", process.env.OPK_PUBLIC_BASE_URL);
}

export async function searchPublicPortal(query: string): Promise<PortalSearchResult[]> {
  const normalized = query.trim();
  if (normalized.length < 1 || normalized.length > 200) {
    throw new Error("Search query must contain between 1 and 200 characters.");
  }
  if (getPortalDataMode() !== "live") return [];
  const apiBaseUrl = parseBaseUrl("OPK_API_BASE_URL", process.env.OPK_API_BASE_URL);
  const publicBaseUrl = getConfiguredPublicBaseUrl()!;
  const url = new URL("/api/public/search", apiBaseUrl);
  url.searchParams.set("q", normalized);
  url.searchParams.set("limit", "20");
  const response = await fetch(url, {
    headers: { Accept: "application/json" },
    cache: "no-store",
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`Public search returned HTTP ${response.status}.`);
  const value: unknown = await response.json();
  if (!isRecord(value) || !Array.isArray(value.items) || value.items.length > PAGE_SIZE) {
    throw new Error("Public search returned an invalid result envelope.");
  }
  return value.items.map((item, index) => parseSearchResult(item, index, publicBaseUrl));
}

async function fetchPublicList(baseUrl: URL, pathname: string): Promise<PublicList> {
  const url = new URL(pathname, baseUrl);
  url.searchParams.set("limit", String(PAGE_SIZE));
  const response = await fetch(url, {
    headers: { Accept: "application/json" },
    next: { revalidate: 60 },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`${pathname} returned HTTP ${response.status}.`);
  return parsePublicList(await response.json(), pathname);
}

async function fetchPublicDatasets(apiBaseUrl: URL, publicBaseUrl: URL): Promise<PortalDataset[]> {
  const response = await fetch(new URL("/api/public/datasets", apiBaseUrl), {
    headers: { Accept: "application/json" },
    next: { revalidate: 60 },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });
  if (!response.ok) throw new Error(`/api/public/datasets returned HTTP ${response.status}.`);
  const value: unknown = await response.json();
  if (!Array.isArray(value) || value.length > 100) {
    throw new Error("Public dataset catalog returned an invalid response.");
  }
  return value.map((item, index) => {
    const source = `/api/public/datasets[${index}]`;
    if (!isRecord(item)) throw new Error(`${source} must be an object.`);
    const code = requiredString(item.code, `${source}.code`);
    if (!/^[a-z0-9][a-z0-9_-]{0,99}$/.test(code)) throw new Error(`${source}.code is invalid.`);
    const updatedAt = requiredString(item.updatedAt, `${source}.updatedAt`);
    if (!Number.isFinite(Date.parse(updatedAt))) throw new Error(`${source}.updatedAt must be an ISO date.`);
    return {
      code,
      name: requiredString(item.name, `${source}.name`),
      description: requiredString(item.description, `${source}.description`),
      updatedAt: dateFormatter.format(new Date(updatedAt)),
      href: new URL(`/api/public/datasets/${code}`, publicBaseUrl).toString(),
    };
  });
}

function parseSearchResult(value: unknown, index: number, publicBaseUrl: URL): PortalSearchResult {
  const source = `/api/public/search.items[${index}]`;
  if (!isRecord(value)) throw new Error(`${source} must be an object.`);
  const rawUrl = requiredString(value.url, `${source}.url`);
  const path = new URL(rawUrl, publicBaseUrl).pathname;
  const allowed = ["/content/", "/pages/", "/api/public/datasets/"];
  if (!allowed.some((prefix) => path.startsWith(prefix))) {
    throw new Error(`${source}.url is outside public search boundaries.`);
  }
  return {
    id: requiredString(value.targetId, `${source}.targetId`),
    title: requiredString(value.title, `${source}.title`),
    summary: typeof value.summary === "string" ? value.summary : "",
    type: requiredString(value.targetType, `${source}.targetType`),
    href: new URL(path, publicBaseUrl).toString(),
  };
}

function parsePublicList(value: unknown, source: string): PublicList {
  if (!isRecord(value) || !Array.isArray(value.items) || typeof value.hasMore !== "boolean") {
    throw new Error(`${source} returned an invalid list envelope.`);
  }
  if (value.items.length > PAGE_SIZE) throw new Error(`${source} exceeded the requested page size.`);
  return {
    hasMore: value.hasMore,
    items: value.items.map((item, index) => parsePublicItem(item, `${source}.items[${index}]`)),
  };
}

function parsePublicItem(value: unknown, source: string): PublicListItem {
  if (!isRecord(value)) throw new Error(`${source} must be an object.`);
  const id = requiredString(value.id, `${source}.id`);
  const title = requiredString(value.title, `${source}.title`);
  const canonicalUrl = requiredString(value.canonicalUrl, `${source}.canonicalUrl`);
  const publishedAt = requiredString(value.publishedAt, `${source}.publishedAt`);
  if (!Number.isFinite(Date.parse(publishedAt))) throw new Error(`${source}.publishedAt must be an ISO date.`);
  const canonical = new URL(canonicalUrl);
  if (canonical.protocol !== "http:" && canonical.protocol !== "https:") {
    throw new Error(`${source}.canonicalUrl must use HTTP or HTTPS.`);
  }
  return {
    id,
    title,
    canonicalUrl,
    publishedAt,
    contentType: typeof value.contentType === "string" ? value.contentType : undefined,
  };
}

function toPublication(item: PublicListItem, publicBaseUrl: URL, isPage: boolean) {
  const publishedAt = new Date(item.publishedAt);
  const canonicalPath = new URL(item.canonicalUrl).pathname;
  const expectedPrefix = isPage ? "/pages/" : "/content/";
  if (!canonicalPath.startsWith(expectedPrefix)) {
    throw new Error(`Public API canonical path must start with ${expectedPrefix}.`);
  }
  const href = new URL(canonicalPath, publicBaseUrl).toString();
  const format = isPage ? "Page" as const : mapContentType(item.contentType);
  return {
    id: item.id,
    title: item.title,
    category: isPage ? "Page" : item.contentType || "Content",
    date: dateFormatter.format(publishedAt),
    freshness: "Published",
    format,
    href,
    timestamp: publishedAt.getTime(),
  };
}

function mapContentType(contentType?: string): ExamplePublication["format"] {
  const normalized = contentType?.toLowerCase() || "";
  if (normalized.includes("dataset") || normalized.includes("data")) return "Dataset";
  if (normalized.includes("event") || normalized.includes("activity")) return "Event";
  if (normalized.includes("report")) return "Report";
  return "Article";
}

function parseBaseUrl(name: string, value?: string): URL {
  if (!value?.trim()) throw new Error(`${name} is required when OPK_WEB_DATA_MODE=live.`);
  const url = new URL(value);
  if ((url.protocol !== "http:" && url.protocol !== "https:") || url.username || url.password ||
      url.hash || url.search || url.pathname !== "/") {
    throw new Error(`${name} must be an HTTP(S) origin without credentials, a path, a query, or a fragment.`);
  }
  return url;
}

function formatCount(count: number, hasMore: boolean) {
  return hasMore ? `${count}+` : String(count);
}

function requiredString(value: unknown, name: string) {
  if (typeof value !== "string" || !value.trim()) throw new Error(`${name} must be a non-empty string.`);
  return value;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
});
