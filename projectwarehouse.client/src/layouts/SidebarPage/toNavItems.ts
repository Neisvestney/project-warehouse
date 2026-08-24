import type {PermissionName} from "@/api/types.gen";
import type {
  SidebarNavGroup,
  SidebarNavItem,
  SidebarNavLeafItem,
} from "@/layouts/SidebarLayout/navItems.ts";
import type {SectionConfig} from "./SidebarPage.tsx";
import {isSectionVisible} from "./sectionVisibility.ts";

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
          defaultPath: visibleChildren[0].path,
          children: visibleChildren,
          icon: s.icon,
        } as SidebarNavGroup;
      }
      return {label: s.label, path: absPath, icon: s.icon} as SidebarNavLeafItem;
    })
    .filter(Boolean) as SidebarNavItem[];
}
