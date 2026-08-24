import React from "react";
import {Navigate, Route, Routes} from "react-router";
import type {PermissionName} from "@/api/types.gen";
import {useAuth} from "@/hooks/useAuth";
import AccessDenied from "@/components/AccessDenied";
import SidebarLayout from "@/layouts/SidebarLayout/SidebarLayout.tsx";
import {hasSectionPermission, isSectionVisible} from "./sectionVisibility.ts";
import {toNavItems} from "./toNavItems.ts";

export interface SectionSubroute {
  path: string;
  component: React.ComponentType;
}

export interface SectionConfig {
  label: string;
  path: string;
  icon?: React.ReactElement;
  component?: React.ComponentType;
  /** An array means any one of them is enough. */
  requiredPermission?: PermissionName | PermissionName[];
  showIf?: () => boolean;
  subroutes?: SectionSubroute[];
  children?: Omit<SectionConfig, "children">[];
}

export interface SidebarPageProps {
  sections: SectionConfig[];
  basePath: string;
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

function ProtectedRoute({
  section,
  permissions,
}: {
  section: Omit<SectionConfig, "children"> | SectionSubroute;
  permissions: PermissionName[];
}) {
  return !section.component ||
    ("requiredPermission" in section && !hasSectionPermission(section, permissions)) ? (
    <AccessDenied />
  ) : (
    <section.component />
  );
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
        <Route
          key={relativePath}
          path={relativePath}
          element={<ProtectedRoute section={s} permissions={permissions} />}
        />
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
          element={<ProtectedRoute section={sr} permissions={permissions} />}
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
