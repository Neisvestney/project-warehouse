import React from "react";
import AccountBalanceWalletIcon from "@mui/icons-material/AccountBalanceWallet";
import FormatListNumberedIcon from "@mui/icons-material/FormatListNumbered";
import TableChartIcon from "@mui/icons-material/TableChart";
import type {SectionConfig} from "@/navigation/navSection.ts";

export const analyticsSections: SectionConfig[] = [
  {
    label: "Сводка по каналам",
    path: "channels",
    icon: <TableChartIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/ChannelsSummaryPage/ChannelsSummaryPage.tsx")),
    requiredPermission: "analytics.view",
  },
  {
    label: "Выплаты маркетплейсов",
    path: "payouts",
    icon: <AccountBalanceWalletIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/PayoutsPage/PayoutsPage.tsx")),
    requiredPermission: "analytics.view",
  },
  {
    label: "ABC / XYZ",
    path: "abc",
    icon: <FormatListNumberedIcon fontSize="small" />,
    component: React.lazy(() => import("./pages/AbcPage/AbcPage.tsx")),
    requiredPermission: "analytics.view",
  },
];
