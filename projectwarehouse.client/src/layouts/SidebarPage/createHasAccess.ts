import type {PermissionName} from "@/api/types.gen";
import type {SectionConfig} from "./SidebarPage.tsx";
import {isSectionVisible} from "./sectionVisibility.ts";

export function createHasAccess(sections: SectionConfig[]) {
  return (permissions: PermissionName[]): boolean => {
    const all = sections.flatMap((s) => [s, ...(s.children ?? [])]);
    return all.some((s) => isSectionVisible(s, permissions));
  };
}
