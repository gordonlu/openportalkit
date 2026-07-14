import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  testMatch: "live-dependency-failure.spec.ts",
  outputDir: "/tmp/openportalkit-playwright-live-failure",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: "line",
  use: {
    baseURL: "http://127.0.0.1:3102",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    launchOptions: process.env.PLAYWRIGHT_CHROME_PATH
      ? { executablePath: process.env.PLAYWRIGHT_CHROME_PATH }
      : undefined,
  },
  projects: [
    { name: "live-failure-desktop", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 1000 } } },
    { name: "live-failure-mobile", use: { ...devices["Pixel 7"] } },
  ],
  webServer: [
    {
      command: "PORT=3198 OPK_MOCK_FAILURE=1 node e2e/mock-public-api.mjs",
      url: "http://127.0.0.1:3198/health",
      reuseExistingServer: false,
      timeout: 30_000,
    },
    {
      command: "OPK_WEB_DATA_MODE=live OPK_API_BASE_URL=http://127.0.0.1:3198 OPK_PUBLIC_BASE_URL=https://unavailable.example.test npm run dev -- --hostname 127.0.0.1 --port 3102",
      url: "http://127.0.0.1:3102/examples/corporate",
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
});
