import React from "react";
import {Box, List, ListItemButton, ListItemIcon, ListItemText} from "@mui/material";
import {Link, useLocation} from "react-router";
import {isActive, isGroup} from "./navItems.ts";
import type {SidebarNavGroup, SidebarNavItem, SidebarNavLeafItem} from "./navItems.ts";

export interface SidebarLayoutProps {
  navItems: SidebarNavItem[];
  children: React.ReactNode;
}

function SidebarDesktopLeaf({
  item,
  locationPathname,
}: {
  item: SidebarNavLeafItem;
  locationPathname: string;
}) {
  return (
    <ListItemButton
      component={Link}
      to={item.path}
      selected={isActive(item.path, locationPathname)}
      sx={{borderRadius: 1}}
    >
      {item.icon && <ListItemIcon sx={{minWidth: 36}}>{item.icon}</ListItemIcon>}
      <ListItemText primary={item.label} />
    </ListItemButton>
  );
}

function SidebarDesktopGroup({
  item,
  locationPathname,
}: {
  item: SidebarNavGroup;
  locationPathname: string;
}) {
  const _groupActive = item.children.some((c) => isActive(c.path, locationPathname));
  return (
    <>
      <ListItemButton
        component={Link}
        to={item.defaultPath}
        // selected={groupActive}
        sx={{borderRadius: 1}}
      >
        {item.icon && <ListItemIcon sx={{minWidth: 36}}>{item.icon}</ListItemIcon>}
        <ListItemText
          primary={item.label}
          slotProps={{primary: {variant: "body2", sx: {fontWeight: 600}}}}
        />
      </ListItemButton>
      {item.children.map((child) => (
        <ListItemButton
          key={child.path}
          component={Link}
          to={child.path}
          selected={isActive(child.path, locationPathname)}
          sx={{pl: 4, borderRadius: 1}}
        >
          {child.icon && <ListItemIcon sx={{minWidth: 36}}>{child.icon}</ListItemIcon>}
          <ListItemText primary={child.label} />
        </ListItemButton>
      ))}
    </>
  );
}

function SidebarLayout({navItems, children}: SidebarLayoutProps) {
  const location = useLocation();

  return (
    <Box sx={{display: "flex", flexDirection: {xs: "column", md: "row"}, gap: 2}}>
      <Box
        component="nav"
        sx={{
          display: {xs: "none", md: "block"},
          width: 200,
          flexShrink: 0,
          borderRight: 1,
          borderColor: "divider",
          pr: 1,
        }}
      >
        <List disablePadding dense>
          {navItems.map((item) =>
            isGroup(item) ? (
              <SidebarDesktopGroup
                key={item.defaultPath}
                item={item}
                locationPathname={location.pathname}
              />
            ) : (
              <SidebarDesktopLeaf
                key={item.path}
                item={item}
                locationPathname={location.pathname}
              />
            ),
          )}
        </List>
      </Box>

      {/* Content */}
      <Box sx={{flexGrow: 1, minWidth: 0}}>{children}</Box>
    </Box>
  );
}

export default SidebarLayout;
