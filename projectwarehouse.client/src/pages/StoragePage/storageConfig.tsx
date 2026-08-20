import React from "react";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import InventoryIcon from "@mui/icons-material/Inventory2";
import SwapVertIcon from "@mui/icons-material/SwapVert";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import WarehousesPage from "@/pages/WarehousesPage/WarehousesPage.tsx";
import InventoryPage from "@/pages/InventoryPage/InventoryPage.tsx";
import WarehouseViewPage from "@/pages/WarehousesPage/pages/WarehouseViewPage/WarehouseViewPage.tsx";
import WarehouseEditPage from "@/pages/WarehousesPage/pages/WarehouseEditPage/WarehouseEditPage.tsx";
import WarehouseNewPage from "@/pages/WarehousesPage/pages/WarehouseNewPage/WarehouseNewPage.tsx";
import WarehouseInventoryPage from "@/pages/WarehousesPage/pages/WarehouseInventoryPage/WarehouseInventoryPage.tsx";
import StoragePlaceInventoryPage from "@/pages/WarehousesPage/pages/StoragePlaceInventoryPage/StoragePlaceInventoryPage.tsx";
import NodeInventoryPage from "@/pages/WarehousesPage/pages/NodeInventoryPage/NodeInventoryPage.tsx";
import StockMovementsPage from "@/pages/StockMovementsPage/StockMovementsPage.tsx";

export const storageSections: SectionConfig[] = [
  {
    label: "Склады",
    path: "warehouses",
    icon: <WarehouseIcon fontSize="small" />,
    component: WarehousesPage,
    requiredPermission: ["warehouses.view", "warehouses.view_assigned"],
    subroutes: [
      {path: "new", component: WarehouseNewPage},
      {path: ":id/edit", component: WarehouseEditPage},
      {path: ":id/inventory", component: WarehouseInventoryPage},
      {
        path: ":warehouseId/storage-places/:storagePlaceId/nodes/:nodeId/inventory",
        component: NodeInventoryPage,
      },
      {
        path: ":warehouseId/storage-places/:storagePlaceId/inventory",
        component: StoragePlaceInventoryPage,
      },
      {path: ":id", component: WarehouseViewPage},
    ],
  },
  {
    label: "Остатки",
    path: "inventory",
    icon: <InventoryIcon fontSize="small" />,
    component: InventoryPage,
    requiredPermission: ["warehouses.view", "warehouses.view_assigned"],
  },
  {
    label: "Движения товаров",
    path: "stock-movements",
    icon: <SwapVertIcon fontSize="small" />,
    component: StockMovementsPage,
    requiredPermission: ["statistics.view", "statistics.view_assigned"],
  },
];

export const hasStorageAccess = createHasAccess(storageSections);
export const getStorageFirstPageUrl = createFirstPageUrl(storageSections);
