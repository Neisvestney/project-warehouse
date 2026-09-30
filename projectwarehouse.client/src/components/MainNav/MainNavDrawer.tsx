import {Box, Divider, Drawer} from "@mui/material";
import SidebarNavTree from "@/layouts/SidebarLayout/SidebarNavTree.tsx";
import type {SidebarNavEntry} from "@/layouts/SidebarLayout/navItems.ts";
import {useBackClosable} from "@/hooks/useBackClosable.ts";
import MainNavBrand from "./MainNavBrand.tsx";

export interface MainNavDrawerProps {
  open: boolean;
  onClose: () => void;
  entries: SidebarNavEntry[];
}

const DRAWER_WIDTH = 280;

function MainNavDrawer({open, onClose, entries}: MainNavDrawerProps) {
  useBackClosable(open, onClose);

  return (
    <Drawer
      anchor="left"
      open={open}
      onClose={onClose}
      slotProps={{paper: {sx: {width: DRAWER_WIDTH}}}}
    >
      <Box sx={{px: 2, py: 1.5}}>
        <MainNavBrand iconFontSize="small" replace onClick={onClose} />
      </Box>
      <Divider />
      <Box component="nav" aria-label="Основная навигация" sx={{py: 1}}>
        <SidebarNavTree entries={entries} replace onNavigate={onClose} />
      </Box>
    </Drawer>
  );
}

export default MainNavDrawer;
