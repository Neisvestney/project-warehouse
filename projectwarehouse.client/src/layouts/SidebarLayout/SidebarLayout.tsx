import React, {useEffect, useRef, useState} from "react";
import {Box, Paper, useMediaQuery, useTheme} from "@mui/material";
import {useLocation} from "react-router";
import {useFloatTop} from "@/hooks/useFloatTop.ts";
import SidebarNavTree from "./SidebarNavTree.tsx";
import type {SidebarNavEntry} from "./navItems.ts";

export const SIDEBAR_WIDTH = 270;
const RAIL_WIDTH = 60;
const PEEK_DELAY_MS = 200;
const RAIL_BELOW_PX = 1700;

export interface SidebarLayoutProps {
  entries: SidebarNavEntry[];
  children: React.ReactNode;
}

function SidebarLayout({entries, children}: SidebarLayoutProps) {
  const theme = useTheme();
  const isRail = useMediaQuery(theme.breakpoints.down(RAIL_BELOW_PX), {noSsr: true});
  const top = useFloatTop(0);
  const {pathname} = useLocation();
  // Pinned to the page it was opened on, so any navigation folds the overlay back — a touch tap on
  // a rail icon fires mouseenter and would otherwise leave it hanging open.
  const [peekPath, setPeekPath] = useState<string | null>(null);
  const peekTimer = useRef<number | undefined>(undefined);
  const expanded = !isRail || peekPath === pathname;

  useEffect(() => () => window.clearTimeout(peekTimer.current), []);

  const openPeek = () => {
    window.clearTimeout(peekTimer.current);
    peekTimer.current = window.setTimeout(() => setPeekPath(pathname), PEEK_DELAY_MS);
  };

  const closePeek = () => {
    window.clearTimeout(peekTimer.current);
    setPeekPath(null);
  };

  return (
    <Box sx={{display: "flex"}}>
      <Box
        sx={{
          display: {xs: "none", md: "block"},
          displayPrint: "none",
          width: isRail ? RAIL_WIDTH : SIDEBAR_WIDTH,
          flexShrink: 0,
        }}
      >
        <Paper
          component="nav"
          aria-label="Основная навигация"
          square
          elevation={isRail && expanded ? 8 : 0}
          onMouseEnter={isRail ? openPeek : undefined}
          onMouseLeave={isRail ? closePeek : undefined}
          onFocus={(e) => {
            if (isRail && e.target.matches(":focus-visible")) setPeekPath(pathname);
          }}
          onBlur={(e) => {
            if (isRail && !e.currentTarget.contains(e.relatedTarget)) closePeek();
          }}
          style={{top}}
          sx={{
            position: "fixed",
            left: 0,
            bottom: 0,
            width: expanded ? SIDEBAR_WIDTH : RAIL_WIDTH,
            py: 1,
            borderRight: 1,
            borderColor: "divider",
            overflowX: "hidden",
            overflowY: "auto",
            scrollbarWidth: "thin",
            zIndex: theme.zIndex.appBar - 1,
            transition: theme.transitions.create(["width", "box-shadow"], {
              duration: theme.transitions.duration.shorter,
            }),
          }}
        >
          <SidebarNavTree entries={entries} rail={!expanded} />
        </Paper>
      </Box>

      <Box sx={{flexGrow: 1, minWidth: 0}}>{children}</Box>
    </Box>
  );
}

export default SidebarLayout;
