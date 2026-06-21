import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

const frontendPort = 5173;
const backendPort = 5230;
const frontendBaseUrl = `http://127.0.0.1:${frontendPort}`;
const backendBaseUrl = `http://127.0.0.1:${backendPort}`;
const frontendRoot = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(frontendRoot, "..", "..");
const backendProjectPath = path.join(repoRoot, "src", "backend", "FortressSouls.Api", "FortressSouls.Api.csproj");

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  timeout: 30_000,
  reporter: "list",
  outputDir: "test-results",
  use: {
    baseURL: frontendBaseUrl,
    headless: true,
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: `dotnet run --project "${backendProjectPath}" --no-launch-profile --urls ${backendBaseUrl}`,
      cwd: repoRoot,
      url: `${backendBaseUrl}/api/health`,
      reuseExistingServer: false,
      timeout: 120_000,
    },
    {
      command: `npm run dev -- --host 127.0.0.1 --port ${frontendPort} --strictPort`,
      cwd: frontendRoot,
      env: {
        FORTRESS_SOULS_API_PROXY_TARGET: backendBaseUrl,
        FORTRESS_SOULS_FRONTEND_PORT: String(frontendPort),
      },
      url: frontendBaseUrl,
      reuseExistingServer: false,
      timeout: 120_000,
    },
  ],
});
