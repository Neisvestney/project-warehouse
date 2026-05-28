import React from "react";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import PeopleIcon from "@mui/icons-material/People";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import RolesSettingsPage from "./pages/RolesSettingsPage/RolesSettingsPage.tsx";
import UsersPage from "@/pages/UsersPage/UsersPage.tsx";
import UserViewPage from "@/pages/UsersPage/pages/UserViewPage/UserViewPage.tsx";
import UserEditPage from "@/pages/UsersPage/pages/UserEditPage/UserEditPage.tsx";
import UserCreatePage from "@/pages/UsersPage/pages/UserCreatePage/UserCreatePage.tsx";

export const settingsSections: SectionConfig[] = [
  {
    label: "Роли",
    path: "roles",
    icon: <AdminPanelSettingsIcon fontSize="small" />,
    component: RolesSettingsPage,
    requiredPermission: "roles.view",
  },
  {
    label: "Сотрудники",
    path: "employees",
    icon: <PeopleIcon fontSize="small" />,
    component: UsersPage,
    requiredPermission: "users.view",
    subroutes: [
      {path: "new", component: UserCreatePage},
      {path: ":id/edit", component: UserEditPage},
      {path: ":id", component: UserViewPage},
    ],
  },
];

export const hasSettingsAccess = createHasAccess(settingsSections);

export const getSettingsFirstPageUrl = createFirstPageUrl(settingsSections);
