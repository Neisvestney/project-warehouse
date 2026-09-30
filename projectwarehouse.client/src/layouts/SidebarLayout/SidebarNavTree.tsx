import React from "react";
import {Collapse, Divider, List, ListItemButton, ListItemIcon, ListItemText} from "@mui/material";
import type {Theme} from "@mui/material/styles";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {Link, useLocation} from "react-router";
import {isActive, isGroup, isSection} from "./navItems.ts";
import type {SidebarNavEntry, SidebarNavGroup, SidebarNavItem} from "./navItems.ts";

// The active section takes the accent outright: the `Mui-selected` plate alone does not stand out
// among a dozen otherwise identical rows.
const navItemSx = {
  borderRadius: 1,
  px: 1.5,
  // Group children change indent between the rail and the panel; this keeps pace with the panel's width.
  transition: (t: Theme) =>
    t.transitions.create(["padding-left", "background-color"], {
      duration: t.transitions.duration.shorter,
    }),
  "&.Mui-selected": {
    "& .MuiListItemIcon-root": {color: "primary.main"},
    "& .MuiListItemText-primary": {color: "primary.main", fontWeight: 600},
  },
};

interface LinkProps {
  replace?: boolean;
  onNavigate?: () => void;
}

export interface SidebarNavTreeProps extends LinkProps {
  entries: SidebarNavEntry[];
  /** Icons only; group children drop their indent so their icons stay inside the rail. */
  rail?: boolean;
}

function NavLink({
  to,
  icon,
  label,
  selected,
  indent,
  replace,
  onNavigate,
}: LinkProps & {
  to: string;
  icon?: React.ReactElement;
  label: string;
  selected: boolean;
  indent?: boolean;
}) {
  return (
    <ListItemButton
      component={Link}
      to={to}
      replace={replace}
      onClick={onNavigate}
      selected={selected}
      sx={[navItemSx, indent ? {pl: 4} : {}]}
    >
      {icon && <ListItemIcon sx={{minWidth: 32}}>{icon}</ListItemIcon>}
      <ListItemText primary={label} slotProps={{primary: {noWrap: true}}} />
    </ListItemButton>
  );
}

function NavItem({
  item,
  pathname,
  rail,
  openGroupKey,
  onToggleGroup,
  ...linkProps
}: LinkProps & {
  item: SidebarNavItem;
  pathname: string;
  rail?: boolean;
  openGroupKey: string | null;
  onToggleGroup: (key: string) => void;
}) {
  if (!isGroup(item)) {
    return (
      <NavLink
        to={item.path}
        icon={item.icon}
        label={item.label}
        selected={isActive(item.path, pathname)}
        {...linkProps}
      />
    );
  }
  return (
    <NavGroup
      item={item}
      pathname={pathname}
      rail={rail}
      open={openGroupKey === item.key}
      onToggle={() => onToggleGroup(item.key)}
      {...linkProps}
    />
  );
}

function NavGroup({
  item,
  pathname,
  rail,
  open,
  onToggle,
  ...linkProps
}: LinkProps & {
  item: SidebarNavGroup;
  pathname: string;
  rail?: boolean;
  open: boolean;
  onToggle: () => void;
}) {
  const hasActiveChild = hasActiveChildIn(item, pathname);

  return (
    <>
      <ListItemButton
        onClick={onToggle}
        aria-expanded={open}
        selected={!open && hasActiveChild}
        sx={navItemSx}
      >
        {item.icon && <ListItemIcon sx={{minWidth: 32}}>{item.icon}</ListItemIcon>}
        <ListItemText primary={item.label} slotProps={{primary: {noWrap: true}}} />
        {!rail && (
          <ExpandMoreIcon
            fontSize="small"
            sx={{
              transition: (t: Theme) =>
                t.transitions.create("transform", {duration: t.transitions.duration.shorter}),
              transform: open ? "rotate(180deg)" : "none",
            }}
          />
        )}
      </ListItemButton>
      <Collapse in={open} timeout="auto" unmountOnExit>
        <List disablePadding dense>
          {item.children.map((child) => (
            <NavLink
              key={child.path}
              to={child.path}
              icon={child.icon}
              label={child.label}
              selected={isActive(child.path, pathname)}
              indent={!rail}
              {...linkProps}
            />
          ))}
        </List>
      </Collapse>
    </>
  );
}

function hasActiveChildIn(group: SidebarNavGroup, pathname: string) {
  return group.children.some((c) => isActive(c.path, pathname));
}

function itemKey(item: SidebarNavItem) {
  return isGroup(item) ? item.key : item.path;
}

// Rows are identical in the rail and the expanded panel, so expanding never moves a row under the pointer.
function SidebarNavTree({entries, rail, ...linkProps}: SidebarNavTreeProps) {
  const {pathname} = useLocation();
  const groups = entries
    .flatMap((entry) => (isSection(entry) ? entry.items : [entry]))
    .filter(isGroup);
  const autoKey = groups.find((g) => hasActiveChildIn(g, pathname))?.key ?? null;
  // At most one group is open. Navigating into a group opens it; navigating to a page outside every group
  // keeps whatever was open. Adjusted during render, so there is no frame with the stale group.
  const [openState, setOpenState] = React.useState({pathname, key: autoKey});
  if (openState.pathname !== pathname) {
    setOpenState({pathname, key: autoKey ?? openState.key});
  }
  const openGroupKey = openState.pathname === pathname ? openState.key : (autoKey ?? openState.key);

  const onToggleGroup = (key: string) =>
    setOpenState({pathname, key: openGroupKey === key ? null : key});
  const groupProps = {openGroupKey, onToggleGroup};

  return (
    <List disablePadding dense sx={{px: 1}}>
      {entries.map((entry, i) => {
        const divider = i > 0 && (isSection(entry) || isSection(entries[i - 1])) && (
          <Divider sx={{my: 0.5}} />
        );
        const items = isSection(entry) ? entry.items : [entry];
        return (
          <React.Fragment key={isSection(entry) ? entry.key : itemKey(entry)}>
            {divider}
            {items.map((item) => (
              <NavItem
                key={itemKey(item)}
                item={item}
                pathname={pathname}
                rail={rail}
                {...groupProps}
                {...linkProps}
              />
            ))}
          </React.Fragment>
        );
      })}
    </List>
  );
}

export default SidebarNavTree;
