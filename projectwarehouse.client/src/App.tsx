import {useRegisterSW} from "virtual:pwa-register/react";
import {Route} from "react-router";
import CssBaseline from "@mui/material/CssBaseline";
import {ThemeProvider} from "@mui/material";
import theme from "@/theme.ts";
import MainLayout from "@/layouts/MainLayout/MainLayout.tsx";
import {SnackbarProvider} from "notistack";
import ServiceWorkerContext from "@/contexts/ServiceWorker/ServiceWorkerContext.ts";
import UpdatePrompt from "@/components/UpdatePrompt.tsx";
import React, {Suspense} from "react";
import {AuthProvider} from "@/contexts/Auth/AuthProvider.tsx";
import {ModalProvider} from "@/contexts/Modal/ModalProvider.tsx";
import {ProtectedRoutes} from "@/components/ProtectedRoute/ProtectedRoutes.tsx";
import ProtectedRoute from "@/components/ProtectedRoute/ProtectedRoute.tsx";
import {QueryErrorHandler} from "@/components/QueryErrorHandler";

const HomePage = React.lazy(() => import("@/pages/HomePage/HomePage.tsx"));
const ScannerPage = React.lazy(() => import("@/pages/ScannerPage/ScannerPage.tsx"));
const LoginPage = React.lazy(() => import("@/pages/LoginPage/LoginPage.tsx"));
const UsersPage = React.lazy(() => import("@/pages/UsersPage/UsersPage.tsx"));
const UserViewPage = React.lazy(() => import("@/pages/UserViewPage/UserViewPage.tsx"));
const UserEditPage = React.lazy(() => import("@/pages/UserEditPage/UserEditPage.tsx"));
const UserCreatePage = React.lazy(() => import("@/pages/UserCreatePage/UserCreatePage.tsx"));

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
          <ModalProvider>
            <QueryErrorHandler />
            <CssBaseline />
            <UpdatePrompt />
            <AuthProvider>
              <Suspense>
                <ProtectedRoutes>
                  <Route path="/login" element={<LoginPage />} />
                  <ProtectedRoute element={<MainLayout />}>
                    <ProtectedRoute path="/" element={<HomePage />} />
                    <ProtectedRoute
                      path="/users"
                      element={<UsersPage />}
                      requiredPermission="users.view"
                    />
                    <ProtectedRoute
                      path="/users/new"
                      element={<UserCreatePage />}
                      requiredPermission="users.create"
                    />
                    <ProtectedRoute
                      path="/users/:id"
                      element={<UserViewPage />}
                      requiredPermission="users.view"
                    />
                    <ProtectedRoute
                      path="/users/:id/edit"
                      element={<UserEditPage />}
                      requiredPermission="users.edit_profile"
                    />
                  </ProtectedRoute>
                  <ProtectedRoute path="/scanner" element={<ScannerPage />} />
                </ProtectedRoutes>
              </Suspense>
            </AuthProvider>
          </ModalProvider>
        </SnackbarProvider>
      </ThemeProvider>
    </ServiceWorkerContext.Provider>
  );
}

export default App;
