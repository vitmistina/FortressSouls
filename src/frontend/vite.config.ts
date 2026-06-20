import path from "node:path";
import os from "node:os";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const tempBuildDir = path.join(os.tmpdir(), "fortress-souls-vite-build");
const tempCacheDir = path.join(os.tmpdir(), "fortress-souls-vite-cache");

export default defineConfig({
  cacheDir: tempCacheDir,
  plugins: [react()],
  build: {
    outDir: tempBuildDir,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://127.0.0.1:5230",
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
  },
});
