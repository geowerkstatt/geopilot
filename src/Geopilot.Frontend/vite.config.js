import { fileURLToPath, URL } from "node:url";
import process from "node:process";

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import viteTsconfigPaths from "vite-tsconfig-paths";
import fs from "fs";
import path from "path";
import mime from "mime-types";

const baseFolder =
  process.env.APPDATA !== undefined && process.env.APPDATA !== ""
    ? `${process.env.APPDATA}/ASP.NET/https`
    : `${process.env.HOME}/.aspnet/https`;

const certificateArg = process.argv.map(arg => arg.match(/--name=(?<value>.+)/i)).filter(Boolean)[0];
const certificateName = certificateArg ? certificateArg.groups.value : process.env.npm_package_name;

if (!certificateName) {
  console.error(
    "Invalid certificate name. Run this script in the context of an npm/yarn script or pass --name=<<app>> explicitly.",
  );
  process.exit(-1);
}

const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

// https://vitejs.dev/config/
// noinspection JSUnusedGlobalSymbols
export default defineConfig({
  plugins: [
    react(),
    viteTsconfigPaths(),
    // Simple middleware to serve markdown files from src/assets/docs
    {
      name: "devPublic",
      apply: "serve",
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          if (!req?.url) {
            return next();
          }

          // Get clean path without query params
          const urlPath = req.url.split("?")[0];
          const relativePath = urlPath.startsWith("/") ? urlPath.substring(1) : urlPath;
          const filePath = path.resolve(process.cwd(), "devPublic", relativePath);

          try {
            if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
              // Use mime-types to determine content type
              const contentType = mime.lookup(filePath) || "application/octet-stream";
              res.setHeader("Content-Type", contentType);

              // Determine if it's a text-based format
              const isText = Boolean(mime.charset(contentType));

              res.end(fs.readFileSync(filePath, isText ? "utf-8" : undefined));
            } else {
              next();
            }
          } catch (e) {
            next();
          }
        });
      },
    },
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    proxy: {
      "^/api/.*": {
        target: "https://localhost:7443/",
        secure: false,
      },
      "^/browser(/.*)?$": {
        target: "https://localhost:7443/",
        secure: false,
      },
      "^/swagger(/.*)?$": {
        target: "https://localhost:7443/",
        secure: false,
      },
      "^/mapservice(/.*)?$": {
        target: "http://localhost:7188/",
        secure: false,
      },
    },
    port: 5173,
    https: {
      key: fs.existsSync(keyFilePath) ? fs.readFileSync(keyFilePath) : null,
      cert: fs.existsSync(certFilePath) ? fs.readFileSync(certFilePath) : null,
    },
  },
  assetsInclude: ["**/*.md"],
});
