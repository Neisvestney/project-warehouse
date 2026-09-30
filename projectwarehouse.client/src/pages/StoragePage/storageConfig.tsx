import React from "react";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import InventoryIcon from "@mui/icons-material/Inventory2";
import SwapVertIcon from "@mui/icons-material/SwapVert";
import TrendingDownIcon from "@mui/icons-material/TrendingDown";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const storageSections: SectionConfig[] = [
  {
    label: "Склады",
    path: "warehouses",
    icon: <WarehouseIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/WarehousesPage/WarehousesPage.tsx")),
    requiredPermission: ["warehouses.view", "warehouses.view_assigned"],
    subroutes: [
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/WarehousesPage/pages/WarehouseNewPage/WarehouseNewPage.tsx"),
        ),
      },
      {
        path: ":id/edit",
        component: React.lazy(
          () => import("./pages/WarehousesPage/pages/WarehouseEditPage/WarehouseEditPage.tsx"),
        ),
      },
      {
        path: ":id/inventory",
        component: React.lazy(
          () =>
            import("./pages/WarehousesPage/pages/WarehouseInventoryPage/WarehouseInventoryPage.tsx"),
        ),
      },
      {
        path: ":id/forecast",
        component: React.lazy(
          () =>
            import("./pages/WarehousesPage/pages/WarehouseForecastPage/WarehouseForecastPage.tsx"),
        ),
      },
      {
        path: ":warehouseId/storage-places/:storagePlaceId/nodes/:nodeId/inventory",
        component: React.lazy(
          () => import("./pages/WarehousesPage/pages/NodeInventoryPage/NodeInventoryPage.tsx"),
        ),
      },
      {
        path: ":warehouseId/storage-places/:storagePlaceId/inventory",
        component: React.lazy(
          () =>
            import("./pages/WarehousesPage/pages/StoragePlaceInventoryPage/StoragePlaceInventoryPage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/WarehousesPage/pages/WarehouseViewPage/WarehouseViewPage.tsx"),
        ),
      },
    ],
  },
  {
    label: "Остатки",
    path: "inventory",
    icon: <InventoryIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/InventoryPage/InventoryPage.tsx")),
    requiredPermission: ["warehouses.view", "warehouses.view_assigned"],
  },
  {
    label: "Движения товаров",
    path: "stock-movements",
    icon: <SwapVertIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/StockMovementsPage/StockMovementsPage.tsx")),
    requiredPermission: ["statistics.view", "statistics.view_assigned"],
  },
  {
    label: "Прогноз остатков",
    path: "forecast",
    icon: <TrendingDownIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/StockForecastPage/StockForecastPage.tsx")),
    requiredPermission: ["statistics.view", "statistics.view_assigned"],
  },
];
