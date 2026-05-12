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
  {
    label: "Тест",
    path: "roles2",
    icon: <AdminPanelSettingsIcon fontSize="small" />,
    children: [
      {
        label: "1",
        path: "test",
        component: RolesSettingsPage,
        icon: <AdminPanelSettingsIcon fontSize="small" />,
      },
      {
        label: "2",
        path: "test2",
        component: RolesSettingsPage,
        icon: <AdminPanelSettingsIcon fontSize="small" />,
        subroutes: [
          {path: ":id", component: RolesSettingsPage},
        ]
      },
    ],
  },
];

export const hasSettingsAccess = createHasAccess(settingsSections);
