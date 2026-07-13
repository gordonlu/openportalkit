import { expect, test } from "@playwright/test";

const sites = ["corporate", "data", "research", "activity", "finance"] as const;

for (const site of sites) {
  test(`${site} portal renders its own product layout`, async ({ page }, testInfo) => {
    const consoleErrors: string[] = [];
    page.on("console", (message) => {
      if (message.type() === "error") consoleErrors.push(message.text());
    });

    await page.goto(`/examples/${site}`);
    await expect(page).toHaveTitle(/OpenPortalKit Example/);
    await expect(page.locator("main")).toHaveClass(new RegExp(`portal-${site}`));
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.locator(".hero-image")).toBeVisible();
    await expect(page.locator(".publication-row").filter({ has: page.locator("h3") })).toHaveCount(4);
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
    expect(consoleErrors).toEqual([]);

    await page.screenshot({ path: `/tmp/openportalkit-${site}-${testInfo.project.name}.png`, fullPage: true });
  });
}

test("content type filter updates the publication result set", async ({ page }) => {
  await page.goto("/examples/corporate");
  await page.getByLabel("Content type").selectOption("Dataset");
  const rows = page.locator(".publication-row").filter({ has: page.locator("h3") });
  await expect(rows).toHaveCount(1);
  await expect(rows).toContainText("Electric drive efficiency benchmark");

  await page.getByLabel("Content type").selectOption("Event");
  await expect(page.getByText("No publications match this content type.")).toBeVisible();
});

test("example switcher navigates between distinct portal templates", async ({ page }) => {
  await page.goto("/examples/corporate");
  await page.getByText("Examples", { exact: true }).click();
  await page.getByRole("link", { name: /Civic Data Exchange/ }).click();
  await expect(page).toHaveURL(/\/examples\/data$/);
  await expect(page.locator("main")).toHaveClass(/portal-data/);
});
