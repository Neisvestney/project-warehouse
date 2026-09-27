import {SidebarPage} from "@/layouts/SidebarPage/SidebarPage.tsx";
import {analyticsSections} from "./analyticsConfig.tsx";

function AnalyticsPage() {
  return <SidebarPage sections={analyticsSections} basePath="/analytics" />;
}

export default AnalyticsPage;
