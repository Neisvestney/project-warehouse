import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "./SidebarPage.tsx";

export function hasSectionPermission(
  s: Pick<SectionConfig, "requiredPermission">,
  permissions: PermissionName[],
): boolean {
  if (!s.requiredPermission) return true;
  const required = Array.isArray(s.requiredPermission)
    ? s.requiredPermission
    : [s.requiredPermission];
  return required.some((p) => permissions.includes(p));
}

export function isSectionVisible(
  s: Pick<SectionConfig, "requiredPermission" | "showIf">,
  permissions: PermissionName[],
): boolean {
  return hasSectionPermission(s, permissions) && (!s.showIf || s.showIf());
}
