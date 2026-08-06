// Раздел «Хранилище» — файлы подсистемы DataFiles, место на диске и размер БД.
// Не путать с src/pages/StoragePage — та про места хранения на складе.
import {Paper, Stack, Tab, Tabs} from "@mui/material";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import PageGenericHeader from "@/components/PageGenericHeader";
import FilesTab from "./FilesTab";
import DatabaseTab from "./DatabaseTab";

type TabKey = "files" | "database";

export default function StorageSettingsPage() {
  const [tab, setTab] = useSyncedWithQueryState<TabKey>(
    "tab",
    (q) => (q === "database" ? "database" : "files"),
    (v) => (v === "files" ? null : v),
  );

  return (
    <Stack spacing={3} sx={{p: 2}}>
      <PageGenericHeader title="Хранилище" />

      <Paper>
        <Tabs value={tab} onChange={(_, v: TabKey) => setTab(v)}>
          <Tab value="files" label="Файлы" />
          <Tab value="database" label="База данных" />
        </Tabs>
      </Paper>

      {tab === "files" ? <FilesTab /> : <DatabaseTab />}
    </Stack>
  );
}
