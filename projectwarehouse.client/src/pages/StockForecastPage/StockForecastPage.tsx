import {Stack} from "@mui/material";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import ForecastBasePage from "@/components/forecast/ForecastBasePage";

function StockForecastPage() {
  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Прогноз остатков"}]} />
      <ForecastBasePage title="Прогноз остатков" />
    </Stack>
  );
}

export default StockForecastPage;
