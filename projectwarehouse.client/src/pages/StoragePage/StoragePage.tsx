import {SidebarPage} from "@/layouts/SidebarPage/SidebarPage.tsx";
import {storageSections} from "./storageConfig.tsx";

function StoragePage() {
  return <SidebarPage sections={storageSections} basePath="/storage" />;
}

export default StoragePage;
