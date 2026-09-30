import React from "react";
import type {PermissionName} from "@/api/types.gen";
import ProtectedRoute from "@/components/ProtectedRoute/ProtectedRoute.tsx";
import PageTitle from "@/components/PageTitle.tsx";
import NavRedirect from "./NavRedirect.tsx";
import {mainNavBlocks} from "./mainNavConfig.tsx";
import type {NavModule, PageComponent, SectionConfig} from "./navSection.ts";

function pageRoute(
  path: string,
  title: string,
  Page: PageComponent,
  requiredPermission?: PermissionName | PermissionName[],
) {
  return (
    <ProtectedRoute
      key={path}
      path={path}
      requiredPermission={requiredPermission}
      element={
        <PageTitle title={title}>
          <Page />
        </PageTitle>
      }
    />
  );
}

function redirectRoute(path: string, sections: SectionConfig[]) {
  return (
    <ProtectedRoute
      key={path}
      path={path}
      element={<NavRedirect sections={sections} basePath={path} />}
    />
  );
}

function sectionRoutes(sections: SectionConfig[], parentPath: string): React.ReactElement[] {
  return sections.flatMap((s) => {
    const path = `${parentPath}/${s.path}`;
    const own = s.component
      ? [pageRoute(path, s.label, s.component, s.requiredPermission)]
      : s.children
        ? [redirectRoute(path, s.children)]
        : [];
    // Subroutes carry no permission of their own and show the section's label until the page sets a title.
    const subroutes = (s.subroutes ?? []).map((sr) =>
      pageRoute(`${path}/${sr.path}`, s.label, sr.component),
    );
    return [...own, ...subroutes, ...(s.children ? sectionRoutes(s.children, path) : [])];
  });
}

function moduleRoutes({basePath, sections}: NavModule): React.ReactElement[] {
  return [
    ...(basePath ? [redirectRoute(basePath, sections)] : []),
    ...sectionRoutes(sections, basePath),
  ];
}

/** Every route the sidebar links to, as `ProtectedRoute` markers for `ProtectedRoutes`. */
export function buildNavRoutes(): React.ReactElement[] {
  return mainNavBlocks.flat().flatMap((node) => moduleRoutes(node.module));
}
