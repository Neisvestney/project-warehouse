import AccountBalanceWalletIcon from "@mui/icons-material/AccountBalanceWallet";
import FormatListNumberedIcon from "@mui/icons-material/FormatListNumbered";
import TableChartIcon from "@mui/icons-material/TableChart";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import AbcPage from "./pages/AbcPage/AbcPage.tsx";
import ChannelsSummaryPage from "./pages/ChannelsSummaryPage/ChannelsSummaryPage.tsx";
import PayoutsPage from "./pages/PayoutsPage/PayoutsPage.tsx";

export const analyticsSections: SectionConfig[] = [
  {
    label: "Сводка по каналам",
    path: "channels",
    icon: <TableChartIcon fontSize="small" />,
    component: ChannelsSummaryPage,
    requiredPermission: "analytics.view",
  },
  {
    label: "Выплаты маркетплейсов",
    path: "payouts",
    icon: <AccountBalanceWalletIcon fontSize="small" />,
    component: PayoutsPage,
    requiredPermission: "analytics.view",
  },
  {
    label: "ABC / XYZ",
    path: "abc",
    icon: <FormatListNumberedIcon fontSize="small" />,
    component: AbcPage,
    requiredPermission: "analytics.view",
  },
];

export const hasAnalyticsAccess = createHasAccess(analyticsSections);
export const getAnalyticsFirstPageUrl = createFirstPageUrl(analyticsSections);
