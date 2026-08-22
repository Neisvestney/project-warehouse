import React, {Suspense, useLayoutEffect} from "react";
import {Container} from "@mui/material";
import {Outlet} from "react-router";
import MainAppBar, {MAIN_APP_BAR_HEIGHT} from "@/components/MainAppBar.tsx";
import {APP_BAR_HEIGHT_VAR} from "@/hooks/useFloatTop.ts";
import RouteFallback from "@/components/RouteFallback.tsx";

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

  return (
    <>
      <MainAppBar />
      <Container maxWidth="xl" sx={{marginTop: 2, paddingBottom: 2}}>
        <Suspense fallback={<RouteFallback />}>
          <Outlet />
        </Suspense>
      </Container>
    </>
  );
}

export default MainAppBarLayout;
