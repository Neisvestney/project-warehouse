import TableChartIcon from "@mui/icons-material/TableChart";
import {createHasAccess} from "@/layouts/SidebarPage/createHasAccess.ts";
import {createFirstPageUrl} from "@/layouts/SidebarPage/createFirstPageUrl.ts";
import type {SectionConfig} from "@/layouts/SidebarPage/SidebarPage.tsx";
import ChannelsSummaryPage from "./pages/ChannelsSummaryPage/ChannelsSummaryPage.tsx";

export const analyticsSections: SectionConfig[] = [
  {
    label: "Сводка по каналам",
    path: "channels",
    icon: <TableChartIcon fontSize="small" />,
    component: ChannelsSummaryPage,
    requiredPermission: "analytics.view",
  },
];

export const hasAnalyticsAccess = createHasAccess(analyticsSections);
export const getAnalyticsFirstPageUrl = createFirstPageUrl(analyticsSections);
