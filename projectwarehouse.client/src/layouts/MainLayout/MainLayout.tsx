import React from "react";
import {Container} from "@mui/material";
import {Outlet} from "react-router";
import MainAppBar from "@/components/MainAppBar.tsx";
import {SearchParamsProvider} from "@/contexts/SearchParams/SearchParamsProvider";

export interface MainLayoutProps {}

function MainLayout({}: MainLayoutProps) {
  return (
    <>
      <MainAppBar />
      <Container maxWidth="xl" sx={{marginTop: 2}}>
        <SearchParamsProvider>
          <Outlet />
        </SearchParamsProvider>
      </Container>
    </>
  );
}

export default MainLayout;
