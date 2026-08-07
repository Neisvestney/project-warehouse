import {useState} from "react";
import {useParams} from "react-router";
import {
  Box,
  Button,
  CircularProgress,
  Menu,
  MenuItem,
  Paper,
  Stack,
  Tab,
  Tabs,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import SyncIcon from "@mui/icons-material/Sync";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  marketplacesGetAccountOptions,
  marketplacesStartSyncMutation,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage, isNotFoundError} from "@/utils/errorUtils";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";
import {useHasPermission} from "@/hooks/usePermission";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import QueryError from "@/components/QueryError";
import MarketplaceStatusChip from "../../components/MarketplaceStatusChip";
import {SYNC_SCOPE_LABELS, hasCapability} from "../../marketplaceUtils";
import AccountOverviewTab from "./AccountOverviewTab";
import AccountWarehousesTab from "./AccountWarehousesTab";
import AccountCardsTab from "./AccountCardsTab";
import AccountSyncRunsTab from "./AccountSyncRunsTab";
import EditAccountDialog from "./EditAccountDialog";
import DeleteAccountDialog from "./DeleteAccountDialog";
import type {MarketplaceSyncScope} from "@/api/types.gen";

const TAB_KEYS = ["overview", "warehouses", "cards", "runs"] as const;
type TabKey = (typeof TAB_KEYS)[number];

// "orders" отсутствует намеренно: заказы тянутся только вручную со страницы FBS
const SYNC_SCOPES: MarketplaceSyncScope[] = ["all", "warehouses", "cards"];

/**
 * Вкладки делят один URL, поэтому у них общие имена параметров. При смене вкладки чистим их все —
 * иначе `page=2` со складов уводит карточки на пустую страницу, а `archived` протекает между ними.
 */
const TAB_SCOPED_PARAMS = [
  "search",
  "page",
  "pageSize",
  "sortBy",
  "sortOrder",
  "archived",
  "mappingState",
  "catalogItem",
];

/** Пока идёт запуск, счётчики тянем опросом — realtime-транспорта на клиенте пока нет. */
const RUNNING_POLL_MS = 3000;

function MarketplaceAccountPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [syncMenuAnchor, setSyncMenuAnchor] = useState<HTMLElement | null>(null);

  const canEdit = useHasPermission("integrations.edit");
  const canMap = useHasPermission("integrations.map");
  const {setParam} = useSearchParamsContext();

  const [tab, setTab] = useSyncedWithQueryState<TabKey>(
    "tab",
    (q) => (TAB_KEYS.includes(q as TabKey) ? (q as TabKey) : "overview"),
    (v) => (v === "overview" ? null : v),
  );

  // setParam копит вызовы одного тика, поэтому чистка и смена вкладки уезжают одной навигацией
  const changeTab = (next: TabKey) => {
    for (const key of TAB_SCOPED_PARAMS) setParam(key, null);
    setTab(next);
  };

  const accountQueryOptions = marketplacesGetAccountOptions({path: {id: id!}});
  const {
    data: account,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...accountQueryOptions,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
    refetchInterval: (query) =>
      query.state.data?.lastSyncStatus === "running" ? RUNNING_POLL_MS : false,
  });

  const syncMutation = useMutation({
    ...marketplacesStartSyncMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      enqueueSnackbar("Синхронизация запущена", {variant: "success"});
      await queryClient.invalidateQueries({queryKey: accountQueryOptions.queryKey});
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось запустить синхронизацию", {
        variant: "error",
      }),
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!account) return <NotFound />;

  const showWarehouses = hasCapability(account.capabilities, "warehouses");
  const showCards = hasCapability(account.capabilities, "cards");
  // Вкладка из URL может указывать на скрытую площадкой возможность
  const activeTab: TabKey =
    (tab === "warehouses" && !showWarehouses) || (tab === "cards" && !showCards) ? "overview" : tab;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Маркетплейсы", link: "/settings/integrations"}, {name: account.name}]}
      />
      <PageGenericHeader
        title={
          <Stack spacing={1} direction="row" sx={{alignItems: "center"}} useFlexGap>
            {account.name}
            <MarketplaceStatusChip status={account.lastSyncStatus} />
          </Stack>
        }
        right={
          <>
            {canMap && (
              <Button
                variant="outlined"
                startIcon={<SyncIcon />}
                disabled={syncMutation.isPending || account.lastSyncStatus === "running"}
                onClick={(e) => setSyncMenuAnchor(e.currentTarget)}
              >
                Синхронизировать
              </Button>
            )}
            {canEdit && (
              <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setEditOpen(true)}>
                Изменить
              </Button>
            )}
            {canEdit && (
              <Button
                variant="outlined"
                color="error"
                startIcon={<DeleteIcon />}
                onClick={() => setDeleteOpen(true)}
              >
                Удалить
              </Button>
            )}
          </>
        }
      />

      <Menu
        anchorEl={syncMenuAnchor}
        open={!!syncMenuAnchor}
        onClose={() => setSyncMenuAnchor(null)}
      >
        {SYNC_SCOPES.map((scope) => (
          <MenuItem
            key={scope}
            onClick={() => {
              setSyncMenuAnchor(null);
              syncMutation.mutate({path: {id: account.id}, body: {scope}});
            }}
          >
            {SYNC_SCOPE_LABELS[scope]}
          </MenuItem>
        ))}
      </Menu>

      <Paper>
        <Tabs
          value={activeTab}
          onChange={(_, v: TabKey) => changeTab(v)}
          variant="scrollable"
          scrollButtons="auto"
        >
          <Tab value="overview" label="Обзор" />
          {showWarehouses && <Tab value="warehouses" label="Склады" />}
          {showCards && <Tab value="cards" label="Карточки" />}
          <Tab value="runs" label="История" />
        </Tabs>
      </Paper>

      {activeTab === "overview" && <AccountOverviewTab account={account} />}
      {activeTab === "warehouses" && <AccountWarehousesTab accountId={account.id} />}
      {activeTab === "cards" && (
        <CatalogItemDrawerHost>
          <AccountCardsTab accountId={account.id} />
        </CatalogItemDrawerHost>
      )}
      {activeTab === "runs" && (
        <AccountSyncRunsTab
          accountId={account.id}
          isRunning={account.lastSyncStatus === "running"}
        />
      )}

      <EditAccountDialog open={editOpen} account={account} onClose={() => setEditOpen(false)} />
      <DeleteAccountDialog
        open={deleteOpen}
        accountId={account.id}
        accountName={account.name}
        onClose={() => setDeleteOpen(false)}
      />
    </Stack>
  );
}

export default MarketplaceAccountPage;
