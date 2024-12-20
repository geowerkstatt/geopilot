import { fileURLToPath, URL } from "node:url";
import process from "node:process";

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import viteTsconfigPaths from "vite-tsconfig-paths";
import fs from "fs";
import path from "path";

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
    https: {
      key: fs.existsSync(keyFilePath) ? fs.readFileSync(keyFilePath) : null,
      cert: fs.existsSync(certFilePath) ? fs.readFileSync(certFilePath) : null,
    },
  },
  assetsInclude: ["**/*.md"],
});
