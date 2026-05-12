import React from "react";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import RolesSettingsPage from "./pages/RolesSettingsPage/RolesSettingsPage.tsx";

export const settingsSections: SectionConfig[] = [
  {
    label: "Роли",
    path: "roles",
    icon: <AdminPanelSettingsIcon fontSize="small" />,
    component: RolesSettingsPage,
  },
];

export const hasSettingsAccess = createHasAccess(settingsSections);
