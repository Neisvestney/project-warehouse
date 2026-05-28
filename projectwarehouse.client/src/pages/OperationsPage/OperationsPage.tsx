import {SidebarPage} from "@/layouts/SidebarPage/SidebarPage.tsx";
import {operationsSections} from "./operationsConfig.tsx";

function OperationsPage() {
  return <SidebarPage sections={operationsSections} basePath="/operations" />;
}

export default OperationsPage;
