import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "./SidebarPage.tsx";
import {isSectionVisible} from "./sectionVisibility.ts";

export function createFirstPageUrl(sections: SectionConfig[]) {
  return (permissions: PermissionName[]): string => {
    const all = sections.flatMap((s) => [
      ...(s.children ? s.children.map((x) => ({...x, path: `${s.path}/${x.path}`})) : [s]),
    ]);
    const first = all.find((s) => isSectionVisible(s, permissions));
    return first?.path ?? "";
  };
}
