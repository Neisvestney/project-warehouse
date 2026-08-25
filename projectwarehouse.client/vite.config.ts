import {fileURLToPath, URL} from "node:url";

import {defineConfig} from "vite";
import plugin, {reactCompilerPreset} from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";
import legacy from "@vitejs/plugin-legacy";
import {VitePWA} from "vite-plugin-pwa";
import fs from "fs";
import path from "path";
import child_process from "child_process";
import {type CodeSplittingGroup} from "rolldown";

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

// Priority orders dependencies before their dependents: a group claims its captured modules'
// dependencies too, so react/emotion must be taken before mui, and mui before the icon packages.
const vendorGroups: CodeSplittingGroup[] = [
  {
    name: "vendor-react",
    test: /node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/,
    priority: 60,
  },
  {name: "vendor-emotion", test: /node_modules[\\/]@emotion[\\/]/, priority: 55},
  {name: "vendor-mui", test: /node_modules[\\/]@mui[\\/](?!x-|icons-material)/, priority: 50},
  {name: "vendor-mui-x", test: /node_modules[\\/]@mui[\\/]x-/, priority: 40},
  {
    name: "vendor-mui-icons",
    test: /node_modules[\\/](@mui[\\/]icons-material|mdi-material-ui)[\\/]/,
    priority: 40,
  },
  {name: "vendor-query", test: /node_modules[\\/]@tanstack[\\/]/, priority: 30},
  {name: "vendor-mobx", test: /node_modules[\\/]mobx(-react-lite)?[\\/]/, priority: 30},
  {name: "vendor-dnd", test: /node_modules[\\/]@dnd-kit[\\/]/, priority: 30},
  {name: "vendor-capacitor", test: /node_modules[\\/]@capacitor[\\/]/, priority: 30},
  {name: "vendor", test: /node_modules/, priority: 10},
].map((group) => ({...group, tags: ["$initial"]}));

const sharedDirs =
  "api|components|configuration|contexts|features|hooks|layouts|plugins|services|utils";

// One chunk per shared module, so editing a widely used component invalidates only its own file
// instead of every page chunk that inlined it.
const sharedGroup: CodeSplittingGroup = {
  name: (moduleId) =>
    "app-" +
    moduleId
      .replace(/\\/g, "/")
      .replace(/^.*\/src\//, "")
      .replace(/\.[^./]+$/, "")
      .replace(/\//g, "-"),
  test: new RegExp(`[\\\\/]src[\\\\/](${sharedDirs})[\\\\/]`),
  minShareCount: 2,
  priority: 5,
};

// https://vitejs.dev/config/
export default defineConfig(({command}) => ({
  plugins: [
    plugin(),
    babel({presets: [reactCompilerPreset()]}),
    legacy({
      targets: ["chrome >= 49", "android >= 49"],
    }),
    VitePWA({
      registerType: "prompt",
      workbox: {
        // add this to cache all the imports
        globPatterns: ["**/*"],
        // Legacy bundles are served only to browsers that skip the module build, so precaching them
        // would double the payload for everyone else; they are cached on demand instead.
        globIgnores: ["**/node_modules/**/*", "**/*-legacy-*.js"],
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            urlPattern: /^\/api\//,
            handler: "NetworkOnly",
          },
          {
            urlPattern: /-legacy-[^/]*\.js$/,
            handler: "CacheFirst",
            options: {
              cacheName: "legacy-assets",
              expiration: {maxEntries: 60, maxAgeSeconds: 60 * 60 * 24 * 30},
              cacheableResponse: {statuses: [0, 200]},
            },
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
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [...vendorGroups, sharedGroup],
        },
      },
    },
  },
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
    https: {
      key: fs.readFileSync(keyFilePath),
      cert: fs.readFileSync(certFilePath),
    },
  },
}));
