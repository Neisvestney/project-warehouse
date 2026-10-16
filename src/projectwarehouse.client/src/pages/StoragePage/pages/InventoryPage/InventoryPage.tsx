import {Stack} from "@mui/material";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import ItemsBasePage from "@/components/inventory/ItemsBasePage";

function InventoryPage() {
  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Остатки"}]} />
      <ItemsBasePage title="Остатки" />
    </Stack>
  );
}

export default InventoryPage;
