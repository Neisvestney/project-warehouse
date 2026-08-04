import React from "react";
import MoveToInboxIcon from "@mui/icons-material/MoveToInbox";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import AssemblyIcon from "@mui/icons-material/Handyman";
import StorefrontIcon from "@mui/icons-material/Storefront";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import WarehouseIcon from "@mui/icons-material/Warehouse";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import ReceiptsPage from "./pages/ReceiptsPage/ReceiptsPage.tsx";
import ReceiptCreatePage from "./pages/ReceiptsPage/pages/ReceiptCreatePage/ReceiptCreatePage.tsx";
import ReceiptPage from "./pages/ReceiptsPage/pages/ReceiptPage/ReceiptPage.tsx";
import TransfersPage from "./pages/TransfersPage/TransfersPage.tsx";
import WriteoffsPage from "./pages/WriteoffsPage/WriteoffsPage.tsx";
import WriteoffCreatePage from "./pages/WriteoffsPage/pages/WriteoffCreatePage/WriteoffCreatePage.tsx";
import WriteoffPage from "./pages/WriteoffsPage/pages/WriteoffPage/WriteoffPage.tsx";
import OrdersDirectPage from "./pages/OrdersDirectPage/OrdersDirectPage.tsx";
import OrdersFbsPage from "./pages/OrdersFbsPage/OrdersFbsPage.tsx";
import OrdersFboPage from "./pages/OrdersFboPage/OrdersFboPage.tsx";
import OrderDirectCreatePage from "./pages/OrderDirectCreatePage/OrderDirectCreatePage.tsx";
import OrderPage from "./pages/OrderPage/OrderPage.tsx";
import OrdersAssemblyPage from "./pages/OrdersAssemblyPage/OrdersAssemblyPage.tsx";

export const operationsSections: SectionConfig[] = [
  {
    label: "Заказы",
    path: "orders",
    icon: <ShoppingCartIcon fontSize="small" />,
    subroutes: [{path: ":id", component: OrderPage}],
    children: [
      {
        label: "Сборка",
        path: "assembly",
        component: OrdersAssemblyPage,
        icon: <AssemblyIcon fontSize="small" />,
      },
      {
        label: "Прямые",
        path: "direct",
        component: OrdersDirectPage,
        subroutes: [{path: "new", component: OrderDirectCreatePage}],
        icon: <StorefrontIcon fontSize="small" />,
      },
      {
        label: "FBS",
        path: "fbs",
        component: OrdersFbsPage,
        icon: <LocalShippingIcon fontSize="small" />,
      },
      {
        label: "FBO",
        path: "fbo",
        component: OrdersFboPage,
        icon: <WarehouseIcon fontSize="small" />,
      },
    ],
  },
  {
    label: "Приемки",
    path: "receipts",
    icon: <MoveToInboxIcon fontSize="small" />,
    component: ReceiptsPage,
    subroutes: [
      {path: "new", component: ReceiptCreatePage},
      {path: ":id", component: ReceiptPage},
    ],
  },
  {
    label: "Перемещения",
    path: "transfers",
    icon: <SwapHorizIcon fontSize="small" />,
    component: TransfersPage,
  },
  {
    label: "Списания",
    path: "writeoffs",
    icon: <DeleteSweepIcon fontSize="small" />,
    component: WriteoffsPage,
    subroutes: [
      {path: "new", component: WriteoffCreatePage},
      {path: ":id", component: WriteoffPage},
    ],
  },
];

export const hasOperationsAccess = createHasAccess(operationsSections);
export const getOperationsFirstPageUrl = createFirstPageUrl(operationsSections);
