import {useRegisterSW} from "virtual:pwa-register/react";
import {Route} from "react-router";
import CssBaseline from "@mui/material/CssBaseline";
import {ThemeProvider} from "@mui/material";
import theme from "@/theme.ts";
import MainLayout from "@/layouts/MainLayout/MainLayout.tsx";
import MainAppBarLayout from "@/layouts/MainAppBarLayout/MainAppBarLayout.tsx";
import {SnackbarProvider} from "notistack";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import UpdatePrompt from "@/components/UpdatePrompt.tsx";
import React, {Suspense} from "react";
import {AuthProvider} from "@/contexts/Auth/AuthProvider.tsx";
import {ModalProvider} from "@/contexts/Modal/ModalProvider.tsx";
import {ProtectedRoutes} from "@/components/ProtectedRoute/ProtectedRoutes.tsx";
import ProtectedRoute from "@/components/ProtectedRoute/ProtectedRoute.tsx";
import {QueryErrorHandler} from "@/components/QueryErrorHandler";
import PageNotFound from "@/components/PageNotFound.tsx";
import {Capacitor} from "@capacitor/core";
import ServerSetupPage from "@/pages/ServerSetupPage/ServerSetupPage.tsx";
import {SELECTED_SERVER_KEY} from "@/configuration/servers.ts";
import ErrorBoundary from "@/components/ErrorBoundary.tsx";
import RouteFallback from "@/components/RouteFallback.tsx";
import {usePeriodicUpdateCheck} from "@/hooks/usePeriodicUpdateCheck.ts";
import TelemetryRouteLogger from "@/components/TelemetryRouteLogger.tsx";

const HomePage = React.lazy(() => import("@/pages/HomePage/HomePage.tsx"));
const MyProfilePage = React.lazy(() => import("@/pages/MyProfilePage/MyProfilePage.tsx"));
const ScannerPage = React.lazy(() => import("@/pages/ScannerPage/ScannerPage.tsx"));
const LoginPage = React.lazy(() => import("@/pages/LoginPage/LoginPage.tsx"));
const SettingsPage = React.lazy(() => import("@/pages/SettingsPage/SettingsPage.tsx"));
const PrintPage = React.lazy(() => import("@/pages/PrintPage/PrintPage.tsx"));
const CatalogPage = React.lazy(() => import("@/pages/CatalogPage/CatalogPage.tsx"));
const StoragePage = React.lazy(() => import("@/pages/StoragePage/StoragePage.tsx"));
const OperationsPage = React.lazy(() => import("@/pages/OperationsPage/OperationsPage.tsx"));
const ThrowErrorPage = React.lazy(() => import("@/pages/ThrowErrorPage/ThrowErrorPage.tsx"));

function App() {
  const [installing, setInstalling] = React.useState(false);

  usePeriodicUpdateCheck();

  const {
    needRefresh: [needRefresh],
    offlineReady: [offlineReady],
    updateServiceWorker,
  } = useRegisterSW({
    onRegistered(registration) {
      if (!registration) return;

      const trackWorker = (worker: ServiceWorker) => {
        setInstalling(true);
        worker.addEventListener(
          "statechange",
          () => {
            if (worker.state === "installed") setInstalling(false);
          },
          {once: true},
        );
      };

      if (registration.installing) trackWorker(registration.installing);

      registration.addEventListener("updatefound", () => {
        if (registration.installing) trackWorker(registration.installing);
      });
    },
    onOfflineReady: () => {
      console.log("onOfflineReady");
    },
  });

  const isLauncher =
    Capacitor.isNativePlatform() &&
    ["capacitor://localhost", "https://localhost"].includes(window.location.origin);

  if (isLauncher && !localStorage.getItem(SELECTED_SERVER_KEY)) {
    return (
      <ThemeProvider theme={theme} defaultMode="system" noSsr>
        <SnackbarProvider>
          <CssBaseline />
          <ServerSetupPage />
        </SnackbarProvider>
      </ThemeProvider>
    );
  }

  return (
    <ServiceWorkerContext.Provider
      value={{installing, needRefresh, offlineReady, updateServiceWorker}}
    >
      <ThemeProvider theme={theme} defaultMode="system" noSsr>
        <ErrorBoundary>
          <SnackbarProvider>
            <ModalProvider>
              <QueryErrorHandler />
              <TelemetryRouteLogger />
              <CssBaseline />
              <UpdatePrompt />
              <AuthProvider>
                <Suspense fallback={<RouteFallback />}>
                  <ProtectedRoutes>
                    <Route path="/server-setup" element={<ServerSetupPage />} />
                    <Route path="/login" element={<LoginPage />} />
                    <ProtectedRoute element={<MainLayout />}>
                      <ProtectedRoute element={<MainAppBarLayout />}>
                        <ProtectedRoute path="/" element={<HomePage />} />
                        <ProtectedRoute path="/profile" element={<MyProfilePage />} />
                        <ProtectedRoute
                          path="/catalog"
                          element={<CatalogPage />}
                          requiredPermission="catalog.view"
                        />
                        <ProtectedRoute path="/storage/*" element={<StoragePage />} />
                        <ProtectedRoute path="/operations/*" element={<OperationsPage />} />
                        <ProtectedRoute path="/settings/*" element={<SettingsPage />} />
                        <ProtectedRoute path="/throw-error" element={<ThrowErrorPage />} />
                        <ProtectedRoute path="*" element={<PageNotFound />} />
                      </ProtectedRoute>
                      <ProtectedRoute path="/scanner" element={<ScannerPage />} />
                      <ProtectedRoute path="/print" element={<PrintPage />} />
                    </ProtectedRoute>
                  </ProtectedRoutes>
                </Suspense>
              </AuthProvider>
            </ModalProvider>
          </SnackbarProvider>
        </ErrorBoundary>
      </ThemeProvider>
    </ServiceWorkerContext.Provider>
  );
}

export default App;
