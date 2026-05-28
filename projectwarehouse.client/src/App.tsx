import {useRegisterSW} from "virtual:pwa-register/react";
import {Route, useLocation, useNavigationType} from "react-router";
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
const InventoryPage = React.lazy(() => import("@/pages/InventoryPage/InventoryPage.tsx"));
const WarehouseInventoryPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/WarehouseInventoryPage/WarehouseInventoryPage.tsx"),
);
const StoragePlaceInventoryPage = React.lazy(
  () =>
    import("@/pages/WarehousesPage/pages/StoragePlaceInventoryPage/StoragePlaceInventoryPage.tsx"),
);
const NodeInventoryPage = React.lazy(
  () => import("@/pages/WarehousesPage/pages/NodeInventoryPage/NodeInventoryPage.tsx"),
);
const ReceiptsPage = React.lazy(() => import("@/pages/ReceiptsPage/ReceiptsPage.tsx"));
const ReceiptCreatePage = React.lazy(
  () => import("@/pages/ReceiptsPage/pages/ReceiptCreatePage/ReceiptCreatePage.tsx"),
);
const ReceiptPage = React.lazy(
  () => import("@/pages/ReceiptsPage/pages/ReceiptPage/ReceiptPage.tsx"),
);

function App() {
  const location = useLocation();
  const navType = useNavigationType();

  console.log(location, navType, window.history);

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
                      path="/inventory"
                      element={<InventoryPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/:id/inventory"
                      element={<WarehouseInventoryPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/:warehouseId/storage-places/:storagePlaceId/inventory"
                      element={<StoragePlaceInventoryPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
                    />
                    <ProtectedRoute
                      path="/warehouses/:warehouseId/storage-places/:storagePlaceId/nodes/:nodeId/inventory"
                      element={<NodeInventoryPage />}
                      requiredPermission={["warehouses.view", "warehouses.view_assigned"]}
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
                      path="/receipts"
                      element={<ReceiptsPage />}
                      requiredPermission={[
                        "receipts.view",
                        "receipts.view_assigned",
                        "receipts.process_assigned",
                      ]}
                    />
                    <ProtectedRoute
                      path="/receipts/new"
                      element={<ReceiptCreatePage />}
                      requiredPermission={["receipts.edit", "receipts.edit_assigned"]}
                    />
                    <ProtectedRoute
                      path="/receipts/:id"
                      element={<ReceiptPage />}
                      requiredPermission={[
                        "receipts.view",
                        "receipts.view_assigned",
                        "receipts.process_assigned",
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
