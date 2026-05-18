import {fileURLToPath, URL} from "node:url";

import {defineConfig} from "vite";
import plugin from "@vitejs/plugin-react";
import legacy from "@vitejs/plugin-legacy";
import {VitePWA} from "vite-plugin-pwa";
import fs from "fs";
import path from "path";
import child_process from "child_process";

const baseFolder =
  process.env.APPDATA !== undefined && process.env.APPDATA !== ""
    ? `${process.env.APPDATA}/ASP.NET/https`
    : `${process.env.HOME}/.aspnet/https`;

const certificateArg = process.argv
  .map((arg) => arg.match(/--name=(?<value>.+)/i))
  .filter(Boolean)[0];
const certificateName =
  certificateArg && certificateArg.groups ? certificateArg.groups.value : "projectwarehouse.client";

if (!certificateName) {
  console.error(
    "Invalid certificate name. Run this script in the context of an npm/yarn script or pass --name=<<app>> explicitly.",
  );
  process.exit(-1);
}

const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
  if (
    0 !==
    child_process.spawnSync(
      "dotnet",
      ["dev-certs", "https", "--export-path", certFilePath, "--format", "Pem", "--no-password"],
      {stdio: "inherit"},
    ).status
  ) {
    throw new Error("Could not create certificate.");
  }
}

// https://vitejs.dev/config/
export default defineConfig(({command}) => ({
  plugins: [
    plugin(),
    legacy({
      targets: ["chrome >= 49", "android >= 49"],
    }),
    VitePWA({
      registerType: "prompt",
      workbox: {
        // add this to cache all the imports
        globPatterns: ["**/*"],
        runtimeCaching: [
          {
            urlPattern: /api/,
            handler: "NetworkOnly",
          },
        ],
      },
      // add this to cache all the
      // static assets in the public folder
      includeAssets: ["**/*"],
      manifest: {
        theme_color: "#1976d2",
        background_color: "#1976d2",
        display: "standalone",
        scope: "/",
        start_url: "/",
        short_name: "Project Warehouse",
        description: "Project Warehouse",
        name: "Project Warehouse",
        icons: [
          {
            src: "/icon-512x512.png",
            sizes: "512x512",
            type: "image/png",
          },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    proxy: {
      "^/api": {
        target: "https://localhost:7095/",
        secure: false,
      },
      "^/openapi": {
        target: "https://localhost:7095/",
        secure: false,
      },
      "^/scalar": {
        target: "https://localhost:7095/",
        secure: false,
      },
    },
    port: 5173,
    // https: {
    //   key: fs.readFileSync(keyFilePath),
    //   cert: fs.readFileSync(certFilePath),
    // },
  },
}));
