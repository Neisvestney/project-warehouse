import React from "react";
import {Box, List, ListItemButton, ListItemIcon, ListItemText, Tab, Tabs} from "@mui/material";
import {Link, matchPath, useLocation} from "react-router";

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

export interface SidebarLayoutProps {
  navItems: SidebarNavItem[];
  children: React.ReactNode;
}

function isGroup(item: SidebarNavItem): item is SidebarNavGroup {
  return "children" in item;
}

function isActive(path: string, locationPathname: string): boolean {
  return !!matchPath({path, end: false}, locationPathname);
}

function activeTabValue(navItems: SidebarNavItem[], locationPathname: string): string | false {
  for (const item of navItems) {
    if (isGroup(item)) {
      if (item.children.some((c) => isActive(c.path, locationPathname))) return item.defaultPath;
    } else {
      if (isActive(item.path, locationPathname)) return item.path;
    }
  }
  return false;
}

function activeTabValueFull(navItems: SidebarNavItem[], locationPathname: string): string | false {
  for (const item of navItems) {
    if (isGroup(item)) {
      for (const child of item.children) {
        if (isActive(child.path, locationPathname)) {
          return child.path;
        }
      }
    } else {
      if (isActive(item.path, locationPathname)) return item.path;
    }
  }
  return false;
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
  const _activeTabPath = activeTabValue(navItems, location.pathname);
  const activeTabPathFull = activeTabValueFull(navItems, location.pathname);

  return (
    <Box sx={{display: "flex", flexDirection: {xs: "column", md: "row"}, gap: 2}}>
      {/* Desktop sidebar */}
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

      {/* Mobile tabs */}
      <Box sx={{display: {xs: "block", md: "none"}}}>
        <Tabs
          value={activeTabPathFull}
          variant="scrollable"
          scrollButtons="auto"
          slotProps={{
            root: {
              sx: {
                minHeight: 30,
              },
            },
          }}
        >
          {navItems.map((item) =>
            isGroup(item) ? (
              item.children.map((child) => (
                <Tab
                  key={child.path}
                  label={child.label}
                  value={child.path}
                  component={Link}
                  to={child.path}
                  sx={{
                    minHeight: 30,
                  }}
                />
              ))
            ) : (
              <Tab
                key={item.path}
                label={item.label}
                value={item.path}
                icon={item.icon}
                iconPosition="start"
                component={Link}
                to={item.path}
                sx={{
                  minHeight: 30,
                }}
              />
            ),
          )}
        </Tabs>
      </Box>

      {/* Content */}
      <Box sx={{flexGrow: 1, minWidth: 0}}>{children}</Box>
    </Box>
  );
}

export default SidebarLayout;
