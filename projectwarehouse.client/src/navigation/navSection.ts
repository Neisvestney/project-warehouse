import type React from "react";
import type {PermissionName} from "@/api/types.gen";
import {hasPermission} from "@/utils/permissions";
import {isGroup} from "@/layouts/SidebarLayout/navItems.ts";
import type {
  SidebarNavGroup,
  SidebarNavItem,
  SidebarNavLeafItem,
} from "@/layouts/SidebarLayout/navItems.ts";

/** Page components are `React.lazy`, so a config import never pulls a page into the entry chunk. */
export type PageComponent = React.LazyExoticComponent<React.ComponentType>;

export interface SectionSubroute {
  path: string;
  component: PageComponent;
}

export interface SectionConfig {
  label: string;
  path: string;
  icon?: React.ReactElement;
  component?: PageComponent;
  /** An array means any one of them is enough. */
  requiredPermission?: PermissionName | PermissionName[];
  showIf?: () => boolean;
  subroutes?: SectionSubroute[];
  children?: Omit<SectionConfig, "children">[];
}

/** A set of sections mounted under one path prefix; `""` puts them at the root. */
export interface NavModule {
  basePath: string;
  sections: SectionConfig[];
}

export function isSectionVisible(
  s: Pick<SectionConfig, "requiredPermission" | "showIf">,
  permissions: PermissionName[],
): boolean {
  return hasPermission(permissions, s.requiredPermission) && (!s.showIf || s.showIf());
}

export function toNavItems(
  sections: SectionConfig[],
  permissions: PermissionName[],
  parentAbsPath: string,
): SidebarNavItem[] {
  return sections
    .filter((s) => isSectionVisible(s, permissions))
    .map((s) => {
      const absPath = `${parentAbsPath}/${s.path}`;
      if (s.children) {
        const visibleChildren = s.children
          .filter((c) => isSectionVisible(c, permissions))
          .map(
            (c) =>
              ({label: c.label, path: `${absPath}/${c.path}`, icon: c.icon}) as SidebarNavLeafItem,
          );
        if (visibleChildren.length === 0) return null;
        return {
          label: s.label,
          key: absPath,
          children: visibleChildren,
          icon: s.icon,
        } as SidebarNavGroup;
      }
      return {label: s.label, path: absPath, icon: s.icon} as SidebarNavLeafItem;
    })
    .filter(Boolean) as SidebarNavItem[];
}

export function firstLeafPath(items: SidebarNavItem[]): string | undefined {
  const first = items[0];
  if (!first) return undefined;
  return isGroup(first) ? first.children[0]?.path : first.path;
}
