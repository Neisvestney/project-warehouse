import {Stack, Typography} from "@mui/material";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";

function RolesSettingsPage() {
  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Настройки", link: "/settings"}, {name: "Роли"}]} />
      <Typography variant="h5">Роли</Typography>
      <Typography color="text.secondary">WIP</Typography>
    </Stack>
  );
}

export default RolesSettingsPage;
