import React from "react";
import {Container} from "@mui/material";
import {Outlet} from "react-router";
import MainAppBar from "@/components/MainAppBar.tsx";
import {RealtimeProvider} from "@/contexts/Realtime/RealtimeProvider";
import {SearchParamsProvider} from "@/contexts/SearchParams/SearchParamsProvider";

export interface MainLayoutProps {}

function MainLayout({}: MainLayoutProps) {
  return (
    <RealtimeProvider>
      <MainAppBar />
      <Container maxWidth="xl" sx={{marginTop: 2, paddingBottom: 2}}>
        <SearchParamsProvider>
          <Outlet />
        </SearchParamsProvider>
      </Container>
    </RealtimeProvider>
  );
}

export default MainLayout;
