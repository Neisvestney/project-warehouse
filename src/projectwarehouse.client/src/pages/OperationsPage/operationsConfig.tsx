import React from "react";
import MoveToInboxIcon from "@mui/icons-material/MoveToInbox";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import AssemblyIcon from "@mui/icons-material/Handyman";
import StorefrontIcon from "@mui/icons-material/Storefront";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import HubIcon from "@mui/icons-material/Hub";
import PalletIcon from "@mui/icons-material/Pallet";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const operationsSections: SectionConfig[] = [
  {
    label: "Заказы",
    path: "orders",
    icon: <ShoppingCartIcon fontSize="small" />,
    subroutes: [
      {path: ":id", component: React.lazy(() => import("./pages/OrderPage/OrderPage.tsx"))},
    ],
    children: [
      {
        label: "Сборка",
        path: "assembly",
        component: React.lazy(() => import("./pages/OrdersAssemblyPage/OrdersAssemblyPage.tsx")),
        icon: <AssemblyIcon fontSize="small" />,
        requiredPermission: ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
      },
      {
        label: "Прямые",
        path: "direct",
        component: React.lazy(() => import("./pages/OrdersDirectPage/OrdersDirectPage.tsx")),
        subroutes: [
          {
            path: "new",
            component: React.lazy(
              () => import("./pages/OrderDirectCreatePage/OrderDirectCreatePage.tsx"),
            ),
          },
        ],
        icon: <StorefrontIcon fontSize="small" />,
        requiredPermission: ["orders.view", "orders.view_assigned"],
      },
      {
        label: "FBS",
        path: "fbs",
        component: React.lazy(() => import("./pages/OrdersFbsPage/OrdersFbsPage.tsx")),
        icon: <LocalShippingIcon fontSize="small" />,
        requiredPermission: ["orders.view", "orders.view_assigned"],
      },
      {
        label: "Отправления FBO",
        path: "fbo",
        component: React.lazy(() => import("./pages/OrdersFboPage/OrdersFboPage.tsx")),
        icon: <HubIcon fontSize="small" />,
        requiredPermission: ["orders.view"],
      },
      {
        label: "Поставки FBO",
        path: "fbo-supply",
        component: React.lazy(() => import("./pages/OrdersFboSupplyPage/OrdersFboSupplyPage.tsx")),
        icon: <PalletIcon fontSize="small" />,
        requiredPermission: ["orders.view", "orders.view_assigned"],
      },
    ],
  },
  {
    label: "Приемки",
    path: "receipts",
    icon: <MoveToInboxIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/ReceiptsPage/ReceiptsPage.tsx")),
    requiredPermission: ["receipts.view", "receipts.view_assigned"],
    subroutes: [
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/ReceiptsPage/pages/ReceiptCreatePage/ReceiptCreatePage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/ReceiptsPage/pages/ReceiptPage/ReceiptPage.tsx"),
        ),
      },
    ],
  },
  {
    label: "Перемещения",
    path: "transfers",
    icon: <SwapHorizIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/TransfersPage/TransfersPage.tsx")),
    requiredPermission: ["transfers.execute", "transfers.execute_assigned"],
  },
  {
    label: "Списания",
    path: "writeoffs",
    icon: <DeleteSweepIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/WriteoffsPage/WriteoffsPage.tsx")),
    requiredPermission: ["writeoffs.view", "writeoffs.view_assigned"],
    subroutes: [
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/WriteoffsPage/pages/WriteoffCreatePage/WriteoffCreatePage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/WriteoffsPage/pages/WriteoffPage/WriteoffPage.tsx"),
        ),
      },
    ],
  },
  {
    label: "Инвентаризации",
    path: "stocktakes",
    icon: <FactCheckIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/StocktakesPage/StocktakesPage.tsx")),
    requiredPermission: ["stocktakes.view", "stocktakes.view_assigned"],
    subroutes: [
      {
        path: "new",
        component: React.lazy(
          () => import("./pages/StocktakesPage/pages/StocktakeCreatePage/StocktakeCreatePage.tsx"),
        ),
      },
      {
        path: ":id",
        component: React.lazy(
          () => import("./pages/StocktakesPage/pages/StocktakePage/StocktakePage.tsx"),
        ),
      },
    ],
  },
];
