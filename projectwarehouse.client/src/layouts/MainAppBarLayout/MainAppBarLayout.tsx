import React, {Suspense, useLayoutEffect} from "react";
import {Container} from "@mui/material";
import {Outlet} from "react-router";
import MainAppBar, {MAIN_APP_BAR_HEIGHT} from "@/components/MainNav/MainAppBar.tsx";
import {APP_BAR_HEIGHT_VAR} from "@/hooks/useFloatTop.ts";
import PageLoader from "@/components/PageLoader.tsx";
import {resolveMainNav} from "@/navigation/mainNavConfig.tsx";
import SidebarLayout, {SIDEBAR_WIDTH} from "@/layouts/SidebarLayout/SidebarLayout.tsx";
import {useAuth} from "@/hooks/useAuth";
import type {PermissionName} from "@/api/types.gen";

// Content fills the whole width beside the sidebar up to a 1920px screen, and stops growing past it.
const CONTENT_MAX_WIDTH = 1920 - SIDEBAR_WIDTH;

export interface MainAppBarLayoutProps {}

function MainAppBarLayout({}: MainAppBarLayoutProps) {
  // Fixed-position overlays live above the router and cannot read the layout tree; the variable is
  // how they learn whether an app bar is occupying the top of the viewport.
  useLayoutEffect(() => {
    const root = document.documentElement;
    root.style.setProperty(APP_BAR_HEIGHT_VAR, `${MAIN_APP_BAR_HEIGHT}px`);
    return () => {
      root.style.removeProperty(APP_BAR_HEIGHT_VAR);
    };
  }, []);

  const {user} = useAuth();
  const navEntries = resolveMainNav((user?.permissions ?? []) as PermissionName[]);

  return (
    <>
      <MainAppBar navEntries={navEntries} />
      <SidebarLayout entries={navEntries}>
        <Container
          maxWidth={false}
          sx={{maxWidth: CONTENT_MAX_WIDTH, marginTop: 2, paddingBottom: 2}}
        >
          <Suspense fallback={<PageLoader />}>
            <Outlet />
          </Suspense>
        </Container>
      </SidebarLayout>
    </>
  );
}

export default MainAppBarLayout;
