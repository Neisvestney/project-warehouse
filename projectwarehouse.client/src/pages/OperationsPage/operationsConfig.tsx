import React from "react";
import MoveToInboxIcon from "@mui/icons-material/MoveToInbox";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import ReceiptsPage from "@/pages/ReceiptsPage/ReceiptsPage.tsx";
import ReceiptCreatePage from "@/pages/ReceiptsPage/pages/ReceiptCreatePage/ReceiptCreatePage.tsx";
import ReceiptPage from "@/pages/ReceiptsPage/pages/ReceiptPage/ReceiptPage.tsx";
import OrdersFbsPage from "./stubs/OrdersFbsPage.tsx";
import OrdersFboPage from "./stubs/OrdersFboPage.tsx";
import TransfersPage from "./stubs/TransfersPage.tsx";
import WriteoffsPage from "./stubs/WriteoffsPage.tsx";

export const operationsSections: SectionConfig[] = [
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
    label: "Заказы",
    path: "orders",
    icon: <ShoppingCartIcon fontSize="small" />,
    children: [
      {label: "FBS", path: "fbs", component: OrdersFbsPage},
      {label: "FBO", path: "fbo", component: OrdersFboPage},
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
  },
];

export const hasOperationsAccess = createHasAccess(operationsSections);
export const getOperationsFirstPageUrl = createFirstPageUrl(operationsSections);
