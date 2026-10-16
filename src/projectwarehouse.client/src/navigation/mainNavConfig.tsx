import React from "react";
import InsightsIcon from "@mui/icons-material/Insights";
import SettingsIcon from "@mui/icons-material/Settings";
import type {PermissionName} from "@/api/types.gen";
import {isGroup} from "@/layouts/SidebarLayout/navItems.ts";
import type {SidebarNavItem, SidebarNavSection} from "@/layouts/SidebarLayout/navItems.ts";
import {catalogSections} from "@/pages/CatalogPage/catalogConfig.tsx";
import {marketplacesSections} from "@/pages/MarketplacesPage/marketplacesConfig.tsx";
import {organizationsSections} from "@/pages/OrganizationsPage/organizationsConfig.tsx";
import {settingsSections} from "@/pages/SettingsPage/settingsConfig.tsx";
import {storageSections} from "@/pages/StoragePage/storageConfig.tsx";
import {operationsSections} from "@/pages/OperationsPage/operationsConfig.tsx";
import {analyticsSections} from "@/pages/AnalyticsPage/analyticsConfig.tsx";
import {toNavItems} from "./navSection.ts";
import type {NavModule} from "./navSection.ts";

/** The module's sections laid out as rows of the block. */
interface MainNavRows {
  kind: "rows";
  module: NavModule;
  /** Hides the rows from the nav only; the routes stay gated by each section's permission. */
  showIf?: (permissions: PermissionName[]) => boolean;
}

/** The module's sections folded into one collapsible group. */
interface MainNavGroup {
  kind: "group";
  label: string;
  icon: React.ReactElement;
  module: NavModule;
}

type MainNavNode = MainNavRows | MainNavGroup;

/**
 * Each inner array is one divider-separated block of the sidebar. The same modules are the route table:
 * `buildNavRoutes` mounts every section declared here.
 */
export const mainNavBlocks: MainNavNode[][] = [
  [{kind: "rows", module: {basePath: "", sections: catalogSections}}],
  [
    {
      kind: "rows",
      module: {basePath: "/storage", sections: storageSections},
      showIf: (p) => p.includes("warehouses.view") || p.includes("warehouses.view_assigned"),
    },
  ],
  [{kind: "rows", module: {basePath: "/operations", sections: operationsSections}}],
  [
    {kind: "rows", module: {basePath: "", sections: marketplacesSections}},
    {kind: "rows", module: {basePath: "", sections: organizationsSections}},
    {
      kind: "group",
      label: "Аналитика",
      icon: <InsightsIcon fontSize="small" />,
      module: {basePath: "/analytics", sections: analyticsSections},
    },
    {
      kind: "group",
      label: "Настройки",
      icon: <SettingsIcon fontSize="small" />,
      module: {basePath: "/settings", sections: settingsSections},
    },
  ],
];

function resolveNode(node: MainNavNode, permissions: PermissionName[]): SidebarNavItem[] {
  const items = toNavItems(node.module.sections, permissions, node.module.basePath);
  if (node.kind === "rows") {
    return !node.showIf || node.showIf(permissions) ? items : [];
  }
  const children = items.flatMap((item) => (isGroup(item) ? item.children : [item]));
  return children.length > 0
    ? [{key: node.module.basePath, label: node.label, icon: node.icon, children}]
    : [];
}

export function resolveMainNav(permissions: PermissionName[]): SidebarNavSection[] {
  return mainNavBlocks
    .map((block, i) => ({
      key: String(i),
      items: block.flatMap((node) => resolveNode(node, permissions)),
    }))
    .filter((section) => section.items.length > 0);
}
