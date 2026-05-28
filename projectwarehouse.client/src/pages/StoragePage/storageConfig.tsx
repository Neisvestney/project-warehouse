import React from "react";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import InventoryIcon from "@mui/icons-material/Inventory2";
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

export const storageSections: SectionConfig[] = [
  {
    label: "Склады",
    path: "warehouses",
    icon: <WarehouseIcon fontSize="small" />,
    component: WarehousesPage,
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
  },
];

export const hasStorageAccess = createHasAccess(storageSections);
export const getStorageFirstPageUrl = createFirstPageUrl(storageSections);
