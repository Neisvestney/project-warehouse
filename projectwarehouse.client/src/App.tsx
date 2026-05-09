import {useRegisterSW} from "virtual:pwa-register/react";
import {Route} from "react-router";
import CssBaseline from "@mui/material/CssBaseline";
import {ThemeProvider} from "@mui/material";
import theme from "@/theme.ts";
import MainLayout from "@/layouts/MainLayout/MainLayout.tsx";
import {SnackbarProvider} from "notistack";
import ServiceWorkerContext from "@/contexts/ServiceWorkerContext.ts";
import UpdatePrompt from "@/components/UpdatePromt/UpdatePrompt.tsx";
import React, {Suspense} from "react";
import {AuthProvider} from "@/contexts/AuthProvider.tsx";
import {ProtectedRoutes} from "@/components/ProtectedRoute/ProtectedRoutes.tsx";
import ProtectedRoute from "@/components/ProtectedRoute/ProtectedRoute.tsx";

const HomePage = React.lazy(() => import("@/pages/HomePage/HomePage.tsx"));
const ScannerPage = React.lazy(() => import("@/pages/ScannerPage/ScannerPage.tsx"));
const LoginPage = React.lazy(() => import("@/pages/LoginPage/LoginPage.tsx"));

function App() {
  const {
    needRefresh: [needRefresh],
    offlineReady: [offlineReady],
    updateServiceWorker,
  } = useRegisterSW({
    onOfflineReady: () => {
      console.log("onOfflineReady");
    },
  });

  return (
    <ServiceWorkerContext.Provider value={{needRefresh, offlineReady, updateServiceWorker}}>
      <ThemeProvider theme={theme}>
        <SnackbarProvider>
          <CssBaseline />
          <UpdatePrompt />
          <AuthProvider>
            <Suspense>
              <ProtectedRoutes>
                <Route path="/login" element={<LoginPage />} />
                <ProtectedRoute element={<MainLayout />}>
                  <ProtectedRoute path="/" element={<HomePage />} />
                  <ProtectedRoute path="/scanner" element={<ScannerPage />} />
                </ProtectedRoute>
              </ProtectedRoutes>
            </Suspense>
          </AuthProvider>
        </SnackbarProvider>
      </ThemeProvider>
    </ServiceWorkerContext.Provider>
  );
}

export default App;
