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
import WarehousesPage from "@/pages/WarehousesPage/WarehousesPage.tsx";
import WarehouseViewPage from "@/pages/WarehousesPage/pages/WarehouseViewPage/WarehouseViewPage.tsx";
const WarehouseItemsPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseItemsPage/WarehouseItemsPage.tsx"),
);

const HomePage = React.lazy(() => import("@/pages/HomePage/HomePage.tsx"));
const MyProfilePage = React.lazy(() => import("@/pages/MyProfilePage/MyProfilePage.tsx"));
const ScannerPage = React.lazy(() => import("@/pages/ScannerPage/ScannerPage.tsx"));
const LoginPage = React.lazy(() => import("@/pages/LoginPage/LoginPage.tsx"));
const UsersPage = React.lazy(() => import("@/pages/UsersPage/UsersPage.tsx"));
const UserViewPage = React.lazy(
  () => import("@/pages/UsersPage/pages/UserViewPage/UserViewPage.tsx"),
);
const UserEditPage = React.lazy(
  () => import("@/pages/UsersPage/pages/UserEditPage/UserEditPage.tsx"),
);
const UserCreatePage = React.lazy(
  () => import("@/pages/UsersPage/pages/UserCreatePage/UserCreatePage.tsx"),
);
const SettingsPage = React.lazy(() => import("@/pages/SettingsPage/SettingsPage.tsx"));
const PrintPage = React.lazy(() => import("@/pages/PrintPage/PrintPage.tsx"));
const CatalogPage = React.lazy(() => import("@/pages/CatalogPage/CatalogPage.tsx"));

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
                    <ProtectedRoute path="/profile" element={<MyProfilePage />} />
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
                    <ProtectedRoute
                      path="/catalog"
                      element={<CatalogPage />}
                      requiredPermission="catalog.view"
                    />
                    <ProtectedRoute
                      path="/warehouses"
                      element={<WarehousesPage />}
                      requiredPermission="warehouses.view"
                    />
                    <ProtectedRoute
                      path="/warehouses/:id"
                      element={<WarehouseViewPage />}
                      requiredPermission="warehouses.view"
                    />
                    <ProtectedRoute
                      path="/warehouses/:id/items"
                      element={<WarehouseItemsPage />}
                      requiredPermission="warehouses.view"
                    />
                    <ProtectedRoute path="/settings/*" element={<SettingsPage />} />
                  </ProtectedRoute>
                  <ProtectedRoute path="/scanner" element={<ScannerPage />} />
                  <ProtectedRoute path="/print" element={<PrintPage />} />
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
