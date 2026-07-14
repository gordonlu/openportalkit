import { expect, test } from "@playwright/test";

test("live mode renders validated public API records and public-origin links", async ({ page }) => {
  await page.goto("/examples/corporate");

  await expect(page.getByText("Live public data", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "OpenPortalKit Examples home" })).toBeVisible();
  await expect(page.locator(".brand-mark")).toHaveText("OPK");
  await expect(page.locator('.primary-nav a[href="#publications"]')).toHaveCount(1);
  if ((page.viewportSize()?.width ?? 1000) <= 820) {
    await page.locator(".mobile-navigation").getByText("Menu", { exact: true }).click();
    await expect(page.locator(".mobile-navigation").getByRole("link", { name: "Publications" })).toBeVisible();
  }
  await expect(page.locator('meta[property="og:image"]')).toHaveAttribute(
    "content",
    "https://public.example.test/examples/corporate.webp",
  );
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
    "href",
    "https://public.example.test/examples/corporate",
  );
  await expect(page.getByRole("link", { name: "Live manufacturing release" })).toHaveAttribute(
    "href",
    "https://public.example.test/content/live-manufacturing-release",
  );
  await expect(page.getByRole("link", { name: "Live governance page" })).toHaveAttribute(
    "href",
    "https://public.example.test/pages/live-governance-page",
  );
  await expect(page.getByText("2026 advanced manufacturing progress report")).toHaveCount(0);
  await expect(page.getByText("Public datasets available").locator("..").getByText("1", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Operations benchmark" })).toHaveAttribute(
    "href",
    "https://public.example.test/api/public/datasets/operations_benchmark",
  );

  await page.getByLabel("Content type").selectOption("Page");
  await expect(page.getByRole("link", { name: "Live governance page" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Live manufacturing release" })).toHaveCount(0);
  await expect(page.locator("footer").getByRole("link", { name: "OpenAPI" })).toHaveAttribute(
    "href",
    "https://public.example.test/api/openapi.json",
  );

  await page.getByLabel("Search content and datasets").fill("operations");
  await page.getByRole("button", { name: "Search" }).click();
  await expect(page.locator(".search-results").getByRole("link", { name: "Operations benchmark" })).toBeVisible();
});
