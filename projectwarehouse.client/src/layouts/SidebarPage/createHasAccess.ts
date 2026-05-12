import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "./SidebarPage.tsx";

export function createHasAccess(sections: SectionConfig[]) {
  return (permissions: PermissionName[]): boolean => {
    const all = sections.flatMap((s) => [s, ...(s.children ?? [])]);
    return all.some(
      (s) =>
        (!s.requiredPermission || permissions.includes(s.requiredPermission)) &&
        (!s.showIf || s.showIf()),
    );
  };
}
