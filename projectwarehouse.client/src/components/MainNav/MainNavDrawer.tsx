import React from "react";
import {
  Box,
  Collapse,
  Divider,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
} from "@mui/material";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import {Link, useLocation} from "react-router";
import {isActive, isGroup} from "@/layouts/SidebarLayout/navItems.ts";
import type {SidebarNavLeafItem} from "@/layouts/SidebarLayout/navItems.ts";
import {useBackClosable} from "@/hooks/useBackClosable.ts";
import type {ResolvedMainNavPage} from "./mainNavConfig.tsx";

export interface MainNavDrawerProps {
  open: boolean;
  onClose: () => void;
  pages: ResolvedMainNavPage[];
}

const DRAWER_WIDTH = 280;

function pageHasActiveItem(page: ResolvedMainNavPage, locationPathname: string): boolean {
  return (
    isActive(page.url, locationPathname) ||
    page.navItems.some((item) =>
      isGroup(item)
        ? item.children.some((c) => isActive(c.path, locationPathname))
        : isActive(item.path, locationPathname),
    )
  );
}

function NavLeaf({
  item,
  depth,
  locationPathname,
  onNavigate,
}: {
  item: SidebarNavLeafItem;
  depth: number;
  locationPathname: string;
  onNavigate: () => void;
}) {
  return (
    <ListItemButton
      component={Link}
      to={item.path}
      replace
      onClick={onNavigate}
      selected={isActive(item.path, locationPathname)}
      sx={{pl: 2 + depth * 2, borderRadius: 1}}
    >
      {item.icon && <ListItemIcon sx={{minWidth: 36}}>{item.icon}</ListItemIcon>}
      <ListItemText primary={item.label} />
    </ListItemButton>
  );
}

function MainNavDrawer({open, onClose, pages}: MainNavDrawerProps) {
  const location = useLocation();
  // undefined means nothing was picked by hand — the section holding the current page is open.
  const [expandedOverride, setExpandedOverride] = React.useState<string | null | undefined>(
    undefined,
  );
  const autoExpanded =
    pages.find((p) => p.navItems.length > 0 && pageHasActiveItem(p, location.pathname))?.name ??
    null;
  const expanded = expandedOverride === undefined ? autoExpanded : expandedOverride;

  const handleClose = () => {
    setExpandedOverride(undefined);
    onClose();
  };

  const toggle = (name: string) => setExpandedOverride(expanded === name ? null : name);

  useBackClosable(open, handleClose);

  return (
    <Drawer
      anchor="left"
      open={open}
      onClose={handleClose}
      slotProps={{paper: {sx: {width: DRAWER_WIDTH}}}}
    >
      <Box sx={{display: "flex", alignItems: "center", gap: 1, px: 2, py: 1.5}}>
        <WarehouseIcon fontSize="small" />
        <Typography
          variant="h6"
          component={Link}
          to="/"
          replace
          onClick={handleClose}
          sx={{
            fontFamily: "monospace",
            fontWeight: 700,
            letterSpacing: "-0.1rem",
            color: "inherit",
            textDecoration: "none",
          }}
        >
          Warehouse
        </Typography>
      </Box>
      <Divider />
      <List component="nav" sx={{px: 1}} dense>
        {pages.map((page) =>
          page.navItems.length === 0 ? (
            <ListItemButton
              key={page.name}
              component={Link}
              to={page.url}
              replace
              onClick={handleClose}
              selected={isActive(page.url, location.pathname)}
              sx={{borderRadius: 1}}
            >
              <ListItemText primary={page.name} slotProps={{primary: {sx: {fontWeight: 600}}}} />
            </ListItemButton>
          ) : (
            <React.Fragment key={page.name}>
              <ListItemButton onClick={() => toggle(page.name)} sx={{borderRadius: 1}}>
                <ListItemText primary={page.name} slotProps={{primary: {sx: {fontWeight: 600}}}} />
                {expanded === page.name ? <ExpandLessIcon /> : <ExpandMoreIcon />}
              </ListItemButton>
              <Collapse in={expanded === page.name} timeout="auto" unmountOnExit>
                <List disablePadding dense>
                  {page.navItems.map((item) =>
                    isGroup(item) ? (
                      <React.Fragment key={item.defaultPath}>
                        <Box sx={{display: "flex", alignItems: "center", pl: 4, py: 0.5}}>
                          {item.icon && (
                            <ListItemIcon sx={{minWidth: 36}}>{item.icon}</ListItemIcon>
                          )}
                          <ListItemText
                            primary={item.label}
                            slotProps={{primary: {color: "text.secondary"}}}
                          />
                        </Box>
                        {item.children.map((child) => (
                          <NavLeaf
                            key={child.path}
                            item={child}
                            depth={2}
                            locationPathname={location.pathname}
                            onNavigate={handleClose}
                          />
                        ))}
                      </React.Fragment>
                    ) : (
                      <NavLeaf
                        key={item.path}
                        item={item}
                        depth={1}
                        locationPathname={location.pathname}
                        onNavigate={handleClose}
                      />
                    ),
                  )}
                </List>
              </Collapse>
            </React.Fragment>
          ),
        )}
      </List>
    </Drawer>
  );
}

export default MainNavDrawer;
