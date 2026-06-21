import path from "node:path";
import os from "node:os";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const tempBuildDir = path.join(os.tmpdir(), "fortress-souls-vite-build");
const tempCacheDir = path.join(os.tmpdir(), "fortress-souls-vite-cache");
const defaultFrontendPort = 5173;
const fallbackApiProxyTarget = "http://127.0.0.1:5230";

function readPort(value: string | undefined, fallback: number): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  if (Number.isNaN(parsed) || parsed <= 0 || parsed > 65535) {
    return fallback;
  }

  return parsed;
}

const frontendPort = readPort(process.env.FORTRESS_SOULS_FRONTEND_PORT, defaultFrontendPort);
const apiProxyTarget = process.env.FORTRESS_SOULS_API_PROXY_TARGET ?? fallbackApiProxyTarget;

export default defineConfig({
  cacheDir: tempCacheDir,
  plugins: [react()],
  build: {
    outDir: tempBuildDir,
  },
  server: {
    port: frontendPort,
    proxy: {
      "/api": {
        target: apiProxyTarget,
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    exclude: ["e2e/**", "**/node_modules/**", "**/dist/**"],
  },
});
