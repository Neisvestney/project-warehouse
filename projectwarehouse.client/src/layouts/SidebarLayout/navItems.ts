import React from "react";
import {matchPath} from "react-router";

export interface SidebarNavLeafItem {
  label: string;
  path: string;
  icon?: React.ReactElement;
}

export interface SidebarNavGroup {
  label: string;
  key: string;
  children: SidebarNavLeafItem[];
  icon?: React.ReactElement;
}

export type SidebarNavItem = SidebarNavLeafItem | SidebarNavGroup;

export interface SidebarNavSection {
  key: string;
  items: SidebarNavItem[];
}

export type SidebarNavEntry = SidebarNavItem | SidebarNavSection;

export function isGroup(item: SidebarNavEntry): item is SidebarNavGroup {
  return "children" in item;
}

export function isSection(entry: SidebarNavEntry): entry is SidebarNavSection {
  return "items" in entry;
}

export function isActive(path: string, locationPathname: string): boolean {
  return !!matchPath({path, end: false}, locationPathname);
}
