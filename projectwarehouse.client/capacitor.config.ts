import type {CapacitorConfig} from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "app.projectwarehouse.client",
  appName: "Project Warehouse",
  webDir: "dist",
  android: {
    allowMixedContent: true,
  },
  server: {
    allowNavigation: ["*"],
  },
};

export default config;
