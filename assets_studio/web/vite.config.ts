import preact from "@preact/preset-vite";
import { defineConfig } from "vite";

// In development the studio API runs separately (just assets_studio studio).
const studio = "http://127.0.0.1:9000";

export default defineConfig({
  plugins: [preact()],
  build: { outDir: "dist", emptyOutDir: true, chunkSizeWarningLimit: 2000 },
  server: { proxy: { "/api": studio, "/mcp": studio } },
});
