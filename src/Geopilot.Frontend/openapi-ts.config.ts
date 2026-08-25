/// <reference types="node" />
import { defineConfig } from "@hey-api/openapi-ts";

// Allow self-signed certificate for openapi spec download.
process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";

export default defineConfig({
  input: {
    path: "https://localhost:5173/swagger/v1/swagger.json",
  },
  parser: {
    filters: {
      operations: {
        exclude: ["/^[A-Z]+ /api/stac(/|$)/"], // example operation name: "GET /api/stac/collections"
      },
    },
  },
  output: {
    path: "./src/api/generated",
    postProcess: ["prettier"],
  },
  plugins: [
    {
      name: "@hey-api/typescript",
      enums: {
        mode: "javascript",
        case: "PascalCase",
      },
    },
  ],
});
