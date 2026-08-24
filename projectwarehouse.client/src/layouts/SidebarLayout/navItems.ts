import React from "react";
import {matchPath} from "react-router";

export interface SidebarNavLeafItem {
  label: string;
  path: string;
  icon?: React.ReactElement;
}

export interface SidebarNavGroup {
  label: string;
  defaultPath: string;
  children: SidebarNavLeafItem[];
  icon?: React.ReactElement;
}

export type SidebarNavItem = SidebarNavLeafItem | SidebarNavGroup;

export function isGroup(item: SidebarNavItem): item is SidebarNavGroup {
  return "children" in item;
}

export function isActive(path: string, locationPathname: string): boolean {
  return !!matchPath({path, end: false}, locationPathname);
}
