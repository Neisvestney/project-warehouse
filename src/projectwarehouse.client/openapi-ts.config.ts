import {defineConfig} from "@hey-api/openapi-ts";

export default defineConfig({
  input: "https://localhost:7095/openapi/v1.json",
  output: {
    path: "src/api",
    postProcess: ["prettier"],
  },
  plugins: [
    "@hey-api/typescript",
    "@hey-api/sdk",
    "@hey-api/client-fetch",
    {
      name: "@tanstack/react-query",
      queryOptions: true,
      mutationOptions: true,
    },
  ],
});
