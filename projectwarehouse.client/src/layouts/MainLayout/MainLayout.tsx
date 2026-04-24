import React from "react";
import {Box, Container} from "@mui/material";
import {Outlet} from "react-router";
import MainAppBar from "../../components/MainAppBar/MainAppBar.tsx";

export interface MainLayoutProps {}

function MainLayout({}: MainLayoutProps) {
  return (
    <>
      <MainAppBar />
      <Container maxWidth="xl" sx={{marginTop: 2}}>
        <Outlet />
      </Container>
    </>
  );
}

export default MainLayout;
