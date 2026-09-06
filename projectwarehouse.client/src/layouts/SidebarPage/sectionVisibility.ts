import type {PermissionName} from "@/api/types.gen";
import {hasPermission} from "@/utils/permissions";
import type {SectionConfig} from "./SidebarPage.tsx";

export function hasSectionPermission(
  s: Pick<SectionConfig, "requiredPermission">,
  permissions: PermissionName[],
): boolean {
  return hasPermission(permissions, s.requiredPermission);
}

export function isSectionVisible(
  s: Pick<SectionConfig, "requiredPermission" | "showIf">,
  permissions: PermissionName[],
): boolean {
  return hasSectionPermission(s, permissions) && (!s.showIf || s.showIf());
}
