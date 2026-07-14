import rawBranding from "@/lib/branding.json";
import projectProfile from "@/lib/project-profile.json";
import { defaultExampleSite } from "@/lib/example-sites";

export type BrandingImage = {
  src: string;
  alt?: string;
  width: number;
  height: number;
};

export type BrandingLink = { label: string; href: string };

export type BrandingManifest = {
  schemaVersion: "opk.branding.v1";
  site: { name: string; shortName: string; description: string; locale: string };
  assets: { logo: BrandingImage | null; favicon: BrandingImage; socialImage: BrandingImage & { alt: string } };
  colors: { accent: string; accentStrong: string; surface: string; surfaceMuted: string; text: string };
  typography: { preset: "editorial" | "modern" | "institutional" };
  navigation: BrandingLink[];
  footer: { copyright: string; links: BrandingLink[] };
};

export const branding = validateBranding(rawBranding);

export function isBrandingActive(siteSlug: string, live: boolean) {
  return live || (projectProfile.projectName !== "OpenPortalKit Examples" && siteSlug === defaultExampleSite);
}

function validateBranding(value: unknown): BrandingManifest {
  if (!isObject(value) || value.schemaVersion !== "opk.branding.v1") throw new Error("Invalid branding schemaVersion.");
  const site = requiredObject(value.site, "site");
  const assets = requiredObject(value.assets, "assets");
  const colors = requiredObject(value.colors, "colors");
  const typography = requiredObject(value.typography, "typography");
  const footer = requiredObject(value.footer, "footer");

  text(site.name, 2, 100, "site.name");
  text(site.shortName, 1, 12, "site.shortName");
  text(site.description, 10, 240, "site.description");
  if (typeof site.locale !== "string" || !/^[a-z]{2}(?:-[A-Z]{2})?$/.test(site.locale)) throw new Error("Invalid branding site.locale.");

  image(assets.favicon, "assets.favicon", false);
  image(assets.socialImage, "assets.socialImage", true);
  if (assets.logo !== null) image(assets.logo, "assets.logo", true);

  for (const key of ["accent", "accentStrong", "surface", "surfaceMuted", "text"] as const) {
    if (typeof colors[key] !== "string" || !/^#[0-9a-fA-F]{6}$/.test(colors[key])) throw new Error(`Invalid branding colors.${key}.`);
  }
  if (!new Set(["editorial", "modern", "institutional"]).has(String(typography.preset))) throw new Error("Invalid branding typography.preset.");

  links(value.navigation, "navigation", 1);
  text(footer.copyright, 2, 160, "footer.copyright");
  links(footer.links, "footer.links", 0);
  return value as BrandingManifest;
}

function image(value: unknown, path: string, altRequired: boolean) {
  const item = requiredObject(value, path);
  if (typeof item.src !== "string" || !/^\/(?!\/)/.test(item.src) || item.src.includes("\\") || /[?#]/.test(item.src)) throw new Error(`Invalid branding ${path}.src.`);
  if (!Number.isInteger(item.width) || Number(item.width) < 1 || Number(item.width) > 4096) throw new Error(`Invalid branding ${path}.width.`);
  if (!Number.isInteger(item.height) || Number(item.height) < 1 || Number(item.height) > 4096) throw new Error(`Invalid branding ${path}.height.`);
  if (altRequired) text(item.alt, 1, 160, `${path}.alt`);
}

function links(value: unknown, path: string, minimum: number) {
  if (!Array.isArray(value) || value.length < minimum || value.length > 8) throw new Error(`Invalid branding ${path}.`);
  for (const [index, entry] of value.entries()) {
    const item = requiredObject(entry, `${path}[${index}]`);
    text(item.label, 1, 40, `${path}[${index}].label`);
    if (typeof item.href !== "string" || !safeHref(item.href)) throw new Error(`Invalid branding ${path}[${index}].href.`);
  }
}

function safeHref(href: string) {
  if (/\p{Cc}/u.test(href) || href.length > 300) return false;
  if (href.startsWith("#")) return href.length > 1 && !href.includes(" ");
  if (href.startsWith("/")) return !href.startsWith("//") && !href.includes("\\");
  try {
    const url = new URL(href);
    return url.protocol === "https:" && url.username === "" && url.password === "";
  } catch {
    return false;
  }
}

function requiredObject(value: unknown, path: string): Record<string, unknown> {
  if (!isObject(value)) throw new Error(`Invalid branding ${path}.`);
  return value;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function text(value: unknown, minimum: number, maximum: number, path: string) {
  if (typeof value !== "string" || value.trim().length < minimum || value.trim().length > maximum || /\p{Cc}/u.test(value)) {
    throw new Error(`Invalid branding ${path}.`);
  }
}
