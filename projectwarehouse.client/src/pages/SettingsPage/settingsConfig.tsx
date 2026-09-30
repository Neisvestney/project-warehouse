import React from "react";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import PeopleIcon from "@mui/icons-material/People";
import StorageIcon from "@mui/icons-material/Storage";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const settingsSections: SectionConfig[] = [
  {
    label: "Роли",
    path: "roles",
    icon: <AdminPanelSettingsIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/RolesSettingsPage/RolesSettingsPage.tsx")),
    requiredPermission: "roles.view",
  },
  {
    label: "Сотрудники",
    path: "employees",
    icon: <PeopleIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/UsersPage/UsersPage.tsx")),
    requiredPermission: "users.view",
    subroutes: [
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/UsersPage/pages/UserCreatePage/UserCreatePage.tsx"),
        ),
      },
      {
        path: ":id/edit",
        component: React.lazy(
          () => import("./pages/UsersPage/pages/UserEditPage/UserEditPage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/UsersPage/pages/UserViewPage/UserViewPage.tsx"),
        ),
      },
    ],
  },
  {
    label: "Теги",
    path: "tags",
    icon: <LocalOfferIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/TagsSettingsPage/TagsSettingsPage.tsx")),
    requiredPermission: "tags.manage",
  },
  {
    label: "Хранилище",
    path: "storage",
    icon: <StorageIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/StorageSettingsPage/StorageSettingsPage.tsx")),
    requiredPermission: "system.view",
  },
];
