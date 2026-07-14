import { expect, test } from "@playwright/test";

test("live mode exposes dependency failure without falling back to demo records", async ({ page }) => {
  await page.goto("/examples/corporate");

  await expect(page.getByText("Public API unavailable", { exact: true })).toBeVisible();
  await expect(page.getByText("No demo records are being shown.", { exact: false })).toBeVisible();
  await expect(page.getByText("Publications are temporarily unavailable.")).toBeVisible();
  await expect(page.getByText("Dataset catalogue is temporarily unavailable.")).toBeVisible();
  await expect(page.getByText("2026 advanced manufacturing progress report")).toHaveCount(0);
  await expect(page.getByLabel("Search content and datasets")).toBeDisabled();
});
