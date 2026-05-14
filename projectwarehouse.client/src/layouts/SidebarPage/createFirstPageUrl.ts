import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "./SidebarPage.tsx";

export function createFirstPageUrl(sections: SectionConfig[]) {
  return (permissions: PermissionName[]): string => {
    const all = sections.flatMap((s) => [
      ...(s.children ? s.children.map((x) => ({...x, path: `${s.path}/${x.path}`})) : [s]),
    ]);
    const first = all.find(
      (s) =>
        (!s.requiredPermission || permissions.includes(s.requiredPermission)) &&
        (!s.showIf || s.showIf()),
    );
    return first?.path ?? "";
  };
}
