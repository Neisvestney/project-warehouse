import React from "react";
import {Navigate, Route, Routes} from "react-router";
import type {PermissionName} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import AccessDenied from "@/components/AccessDenied";
import SidebarLayout, {
  type SidebarNavGroup,
  type SidebarNavItem,
  type SidebarNavLeafItem,
} from "@/layouts/SidebarLayout/SidebarLayout.tsx";

export interface SectionSubroute {
  path: string;
  component: React.ComponentType;
}

export interface SectionConfig {
  label: string;
  path: string;
  icon?: React.ReactElement;
  component?: React.ComponentType;
  requiredPermission?: PermissionName;
  showIf?: () => boolean;
  subroutes?: SectionSubroute[];
  children?: Omit<SectionConfig, "children">[];
}

export interface SidebarPageProps {
  sections: SectionConfig[];
  basePath: string;
}

function isSectionVisible(
  s: Pick<SectionConfig, "requiredPermission" | "showIf">,
  permissions: PermissionName[],
): boolean {
  return (
    (!s.requiredPermission || permissions.includes(s.requiredPermission)) &&
    (!s.showIf || s.showIf())
  );
}

function toNavItems(
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
        } as SidebarNavGroup;
      }
      return {label: s.label, path: absPath, icon: s.icon} as SidebarNavLeafItem;
    })
    .filter(Boolean) as SidebarNavItem[];
}

function RedirectToFirstVisible({
  sections,
  parentAbsPath,
  permissions,
}: {
  sections: Omit<SectionConfig, "children">[];
  parentAbsPath: string;
  permissions: PermissionName[];
}) {
  const first = sections.find((c) => isSectionVisible(c, permissions));
  return <Navigate to={first ? `${parentAbsPath}/${first.path}` : "."} replace />;
}

function buildRoutes(
  sections: SectionConfig[],
  parentAbsPath: string,
  permissions: PermissionName[],
  relativePrefix = "",
): React.ReactElement[] {
  return sections.flatMap((s) => {
    const absPath = `${parentAbsPath}/${s.path}`;
    const relativePath = relativePrefix ? `${relativePrefix}/${s.path}` : s.path;
    return [
      s.component ? (
        <Route key={relativePath} path={relativePath} element={<s.component />} />
      ) : s.children ? (
        <Route
          key={relativePath}
          path={relativePath}
          element={
            <RedirectToFirstVisible
              sections={s.children}
              parentAbsPath={absPath}
              permissions={permissions}
            />
          }
        />
      ) : null,
      ...(s.subroutes ?? []).map((sr) => (
        <Route
          key={`${relativePath}/${sr.path}`}
          path={`${relativePath}/${sr.path}`}
          element={<sr.component />}
        />
      )),
      ...(s.children
        ? buildRoutes(s.children as SectionConfig[], absPath, permissions, relativePath)
        : []),
    ].filter(Boolean) as React.ReactElement[];
  });
}

export function SidebarPage({sections, basePath}: SidebarPageProps) {
  const {user} = useAuth();
  const permissions = (user?.permissions ?? []) as PermissionName[];
  const navItems = toNavItems(sections, permissions, basePath);
  const firstLeaf = navItems.flatMap((item) => ("children" in item ? item.children : [item]))[0];

  return (
    <SidebarLayout navItems={navItems}>
      <Routes>
        <Route
          index
          element={firstLeaf ? <Navigate to={firstLeaf.path} replace /> : <AccessDenied />}
        />
        {buildRoutes(sections, basePath, permissions)}
      </Routes>
    </SidebarLayout>
  );
}
