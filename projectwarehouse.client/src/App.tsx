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
import PageNotFound from "@/components/PageNotFound.tsx";
import {Capacitor} from "@capacitor/core";
import ServerSetupPage from "@/pages/ServerSetupPage/ServerSetupPage.tsx";
import {SELECTED_SERVER_KEY} from "@/configuration/servers.ts";

const WarehousesPage = React.lazy(() => import("@/pages/WarehousesPage/WarehousesPage.tsx"));
const WarehouseViewPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseViewPage/WarehouseViewPage.tsx"),
);
const WarehouseItemsPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseItemsPage/WarehouseItemsPage.tsx"),
);
const WarehouseEditPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseEditPage/WarehouseEditPage.tsx"),
);
const WarehouseNewPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseNewPage/WarehouseNewPage.tsx"),
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
const InboundOrdersPage = React.lazy(
  () => import("@/pages/InboundOrdersPage/InboundOrdersPage.tsx"),
);
const InboundOrderCreatePage = React.lazy(
  () => import("@/pages/InboundOrdersPage/pages/InboundOrderCreatePage/InboundOrderCreatePage.tsx"),
);
const InboundOrderPage = React.lazy(
  () => import("@/pages/InboundOrdersPage/pages/InboundOrderPage/InboundOrderPage.tsx"),
);

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

  const isLauncher =
    Capacitor.isNativePlatform() &&
    ["capacitor://localhost", "https://localhost"].includes(window.location.origin);

  if (isLauncher && !localStorage.getItem(SELECTED_SERVER_KEY)) {
    return (
      <ThemeProvider theme={theme}>
        <SnackbarProvider>
          <CssBaseline />
          <ServerSetupPage />
        </SnackbarProvider>
      </ThemeProvider>
    );
  }

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
                  <Route path="/server-setup" element={<ServerSetupPage />} />
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
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/:id"
                      element={<WarehouseViewPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/:id/items"
                      element={<WarehouseItemsPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/new"
                      element={<WarehouseNewPage />}
                      requiredPermission="warehouses.edit"
                    />
                    <ProtectedRoute
                      path="/warehouses/:id/edit"
                      element={<WarehouseEditPage />}
                      requiredPermission={["warehouses.edit", "warehouses.edit_assigned"]}
                    />
                    <ProtectedRoute
                      path="/inbound-orders"
                      element={<InboundOrdersPage />}
                      requiredPermission={[
                        "inbound_orders.view",
                        "inbound_orders.view_assigned_warehouses",
                      ]}
                    />
                    <ProtectedRoute
                      path="/inbound-orders/new"
                      element={<InboundOrderCreatePage />}
                      requiredPermission={[
                        "inbound_orders.edit",
                        "inbound_orders.edit_assigned_warehouses",
                      ]}
                    />
                    <ProtectedRoute
                      path="/inbound-orders/:id"
                      element={<InboundOrderPage />}
                      requiredPermission={[
                        "inbound_orders.view",
                        "inbound_orders.view_assigned_warehouses",
                      ]}
                    />
                    <ProtectedRoute path="/settings/*" element={<SettingsPage />} />
                    <Route path="*" element={<PageNotFound />} />
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
