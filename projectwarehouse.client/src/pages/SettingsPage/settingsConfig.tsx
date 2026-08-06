import React from "react";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import PeopleIcon from "@mui/icons-material/People";
import StorefrontIcon from "@mui/icons-material/Storefront";
import StorageIcon from "@mui/icons-material/Storage";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import RolesSettingsPage from "./pages/RolesSettingsPage/RolesSettingsPage.tsx";
import UsersPage from "@/pages/UsersPage/UsersPage.tsx";
import UserViewPage from "@/pages/UsersPage/pages/UserViewPage/UserViewPage.tsx";
import UserEditPage from "@/pages/UsersPage/pages/UserEditPage/UserEditPage.tsx";
import UserCreatePage from "@/pages/UsersPage/pages/UserCreatePage/UserCreatePage.tsx";
import MarketplacesSettingsPage from "./pages/MarketplacesSettingsPage/MarketplacesSettingsPage.tsx";
import MarketplaceAccountCreatePage from "./pages/MarketplacesSettingsPage/pages/MarketplaceAccountCreatePage/MarketplaceAccountCreatePage.tsx";
import MarketplaceAccountPage from "./pages/MarketplacesSettingsPage/pages/MarketplaceAccountPage/MarketplaceAccountPage.tsx";
import StorageSettingsPage from "./pages/StorageSettingsPage/StorageSettingsPage.tsx";

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
  {
    label: "Маркетплейсы",
    path: "integrations",
    icon: <StorefrontIcon fontSize="small" />,
    component: MarketplacesSettingsPage,
    requiredPermission: "integrations.view",
    subroutes: [
      {path: "new", component: MarketplaceAccountCreatePage},
      {path: ":id", component: MarketplaceAccountPage},
    ],
  },
  {
    label: "Хранилище",
    path: "storage",
    icon: <StorageIcon fontSize="small" />,
    component: StorageSettingsPage,
    requiredPermission: "system.view",
  },
];

export const hasSettingsAccess = createHasAccess(settingsSections);

export const getSettingsFirstPageUrl = createFirstPageUrl(settingsSections);
