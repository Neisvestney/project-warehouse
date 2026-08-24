import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import {toNavItems} from "@/layouts/SidebarPage/toNavItems.ts";
import type {SidebarNavItem} from "@/layouts/SidebarLayout/navItems.ts";
import {
  getSettingsFirstPageUrl,
  hasSettingsAccess,
  settingsSections,
} from "@/pages/SettingsPage/settingsConfig.tsx";
import {getStorageFirstPageUrl, storageSections} from "@/pages/StoragePage/storageConfig.tsx";
import {
  getOperationsFirstPageUrl,
  operationsSections,
} from "@/pages/OperationsPage/operationsConfig.tsx";

export interface MainNavPage {
  name: string;
  url: string | ((permissions: PermissionName[]) => string);
  requiredPermission?: PermissionName | PermissionName[];
  showIf?: (permissions: PermissionName[]) => boolean;
  basePath?: string;
  sections?: SectionConfig[];
}

export interface ResolvedMainNavPage {
  name: string;
  url: string;
  navItems: SidebarNavItem[];
}

export const mainNavPages: MainNavPage[] = [
  {
    name: "Склад",
    url: (p) => `/storage/${getStorageFirstPageUrl(p)}`,
    showIf: (p) => p.includes("warehouses.view") || p.includes("warehouses.view_assigned"),
    basePath: "/storage",
    sections: storageSections,
  },
  {name: "Каталог", url: "/catalog", requiredPermission: "catalog.view"},
  {
    name: "Операции",
    url: (p) => `/operations/${getOperationsFirstPageUrl(p)}`,
    basePath: "/operations",
    sections: operationsSections,
  },
  {
    name: "Настройки",
    url: (p) => `/settings/${getSettingsFirstPageUrl(p)}`,
    showIf: hasSettingsAccess,
    basePath: "/settings",
    sections: settingsSections,
  },
];

export function resolveMainNavPages(permissions: PermissionName[]): ResolvedMainNavPage[] {
  return mainNavPages
    .filter((page) => {
      const hasPermission =
        !page.requiredPermission ||
        (Array.isArray(page.requiredPermission)
          ? page.requiredPermission.some((p) => permissions.includes(p))
          : permissions.includes(page.requiredPermission));
      return hasPermission && (!page.showIf || page.showIf(permissions));
    })
    .map((page) => ({
      name: page.name,
      url: typeof page.url === "string" ? page.url : page.url(permissions),
      navItems:
        page.sections && page.basePath ? toNavItems(page.sections, permissions, page.basePath) : [],
    }));
}
