import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import viteTsconfigPaths from "vite-tsconfig-paths";

// https://vitejs.dev/config/
// noinspection JSUnusedGlobalSymbols
export default defineConfig({
  plugins: [react(), viteTsconfigPaths()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    proxy: {
      "^/api/.*": {
        target: "http://localhost:7188/",
        secure: false,
      },
      "^/browser(/.*)?$": {
        target: "http://localhost:7188/",
        secure: false,
      },
      "^/swagger(/.*)?$": {
        target: "http://localhost:7188/",
        secure: false,
      },
    },
    port: 5173,
  },
});
