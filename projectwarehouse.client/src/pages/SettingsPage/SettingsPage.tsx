import {SidebarPage} from "@/layouts/SidebarPage/SidebarPage.tsx";
import {settingsSections} from "./settingsConfig.tsx";

export default function SettingsPage() {
  return <SidebarPage sections={settingsSections} basePath="/settings" />;
}
