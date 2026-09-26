import {useCallback, useEffect, useMemo, useState} from "react";
import {
  Alert,
  Box,
  Checkbox,
  CircularProgress,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import PlaylistAddCheckIcon from "@mui/icons-material/PlaylistAddCheck";
import BulkBar, {type BulkAction} from "@/components/BulkBar";
import type {TableInfoStat} from "@/components/TableInfoBar";
import {getTaskProgress} from "@/components/orders/orderAssemblyUtils";
import {useDownloadLabelsAction} from "@/components/orders/marketplace/useDownloadLabelsAction";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {ordersGetAllAssemblyOptions} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {useEntityWatchMany} from "@/hooks/useEntityWatch";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import {byOperation} from "@/utils/queryKeys";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import AssemblyOrderAccordion from "./AssemblyOrderAccordion";
import {checkBatchEligibility, getBatchDisabledReason} from "./batchEligibility";
import BatchAssemblyDialog from "./BatchAssemblyDialog";
import type {SelectedTaskInfo} from "./batchGroups";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import FiltersBar from "@/components/FiltersBar.tsx";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState.ts";
import {useSyncedWithQueryAndStorageState} from "@/hooks/useSyncedWithQueryAndStorageState.ts";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState.ts";
import WarehousesSelect from "@/components/WarehousesSelect.tsx";
import SearchWithItemsInput from "@/components/catalog/SearchWithItemsInput";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import AssemblyOrderGroup from "./AssemblyOrderGroup";
import {
  ASSEMBLY_GROUPING_LABELS,
  ASSEMBLY_GROUPING_STORAGE_KEY,
  type AssemblyGrouping,
  groupAssemblyOrders,
  parseAssemblyGrouping,
} from "./assemblyGrouping";

function OrdersAssemblyPage() {
  const canFulfill = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const [searchInput, setSearchInput, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [warehouseId, setWarehouseId] = useSyncedWithQueryState(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const [catalogItemIds, setCatalogItemIds] = useSyncedWithQueryState<string[]>(
    "item",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const [grouping, setGrouping] = useSyncedWithQueryAndStorageState<AssemblyGrouping>(
    "group",
    ASSEMBLY_GROUPING_STORAGE_KEY,
    parseAssemblyGrouping,
    (v) => v,
  );

  const ordersQuery = useQuery({
    ...ordersGetAllAssemblyOptions({
      query: {
        warehouseId: warehouseId ?? undefined,
        searchString: searchString || undefined,
        catalogItemIds: catalogItemIds.length > 0 ? catalogItemIds : undefined,
        tagIds: tagIds.length > 0 ? tagIds : undefined,
      },
    }),
    gcTime: 0,
  });

  const orders = useMemo<OrderDetailsDto[]>(() => ordersQuery.data ?? [], [ordersQuery.data]);

  // No edit lock here on purpose: several assemblers on one order is the normal case, and they need
  // to see each other's fulfillments instead of a "being edited" banner.
  const queryClient = useQueryClient();
  const groups = groupAssemblyOrders(orders, grouping);
  const orderIds = useMemo(() => orders.map((o) => o.id), [orders]);
  const refreshAssembly = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: byOperation("ordersGetAllAssembly")});
  }, [queryClient]);

  // One refetch per completed subscription set, not one per order: the callback form of
  // useEntityWatchMany fires per id and would invalidate the same key N times on mount.
  const {isWatching} = useEntityWatchMany("order", orderIds);
  useEffect(() => {
    if (isWatching) refreshAssembly();
  }, [isWatching, refreshAssembly]);

  useRealtimeEvent("entityChanged", (_event, payload) => {
    console.log("entityChanged", payload);
    if (payload.entityType === "order" && orderIds.includes(payload.entityId)) refreshAssembly();
  });

  const [isManualRefetching, setIsManualRefetching] = useState(false);
  const showLoading = ordersQuery.isLoading || isManualRefetching;

  async function handleRefresh() {
    setIsManualRefetching(true);
    try {
      await ordersQuery.refetch();
    } finally {
      setIsManualRefetching(false);
    }
  }

  const eligibilityMap = useMemo(() => {
    const m = new Map<string, boolean>();
    for (const order of orders) {
      for (const task of order.assemblyTasks) {
        m.set(task.id, checkBatchEligibility(task));
      }
    }
    return m;
  }, [orders]);

  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(new Set());
  const [batchDialogOpen, setBatchDialogOpen] = useState(false);

  function handleTaskCheckChange(_orderId: string, taskId: string, checked: boolean) {
    setSelectedTaskIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(taskId);
      else next.delete(taskId);
      return next;
    });
  }

  const visibleTaskIds = orders.flatMap((o) => o.assemblyTasks.map((t) => t.id));
  const visibleSelectedCount = visibleTaskIds.filter((id) => selectedTaskIds.has(id)).length;
  const allVisibleSelected =
    visibleTaskIds.length > 0 && visibleSelectedCount === visibleTaskIds.length;

  function handleToggleAllVisible() {
    setSelectedTaskIds((prev) => {
      const next = new Set(prev);
      for (const id of visibleTaskIds) {
        if (allVisibleSelected) next.delete(id);
        else next.add(id);
      }
      return next;
    });
  }

  // Counts come from here, not from selectedTaskIds: ids of tasks hidden by the search/warehouse
  // filter stay in the set, and actions only ever get the visible ones.
  const selectedOrders = orders.filter((o) =>
    o.assemblyTasks.some((t) => selectedTaskIds.has(t.id)),
  );
  const labelOrderIds = selectedOrders.filter((o) => o.marketplaceOrder).map((o) => o.id);

  const batchTaskInfos = useMemo<SelectedTaskInfo[]>(() => {
    const result: SelectedTaskInfo[] = [];
    for (const order of orders) {
      for (const task of order.assemblyTasks) {
        const eligible = eligibilityMap.get(task.id) ?? checkBatchEligibility(task);
        if (selectedTaskIds.has(task.id) && getBatchDisabledReason(task, eligible) === "") {
          result.push({
            orderId: order.id,
            taskId: task.id,
            task,
            // список сборки отдаёт только заказы в статусе Assembly, до которого внешние не доходят
            warehouseId: order.warehouseId!,
            orderNumber: `#${order.number}`,
            postingNumber: order.marketplaceOrder?.postingNumber,
          });
        }
      }
    }
    return result;
  }, [selectedTaskIds, orders, eligibilityMap]);

  const listStats: TableInfoStat[] = (() => {
    const progress = orders
      .flatMap((o) => o.assemblyTasks)
      .reduce(
        (acc, task) => {
          const {fulfilled, total} = getTaskProgress(task);
          return {fulfilled: acc.fulfilled + fulfilled, total: acc.total + total};
        },
        {fulfilled: 0, total: 0},
      );

    return [
      {key: "orders", label: "Заказов на сборке:", value: orders.length.toLocaleString("ru-RU")},
      {
        key: "positions",
        label: "Позиций:",
        value: `${progress.fulfilled.toLocaleString("ru-RU")} из ${progress.total.toLocaleString("ru-RU")}`,
      },
    ];
  })();

  const downloadLabels = useDownloadLabelsAction();

  const selectionActions: BulkAction[] =
    selectedOrders.length > 0
      ? [
          ...(canFulfill && batchTaskInfos.length > 0
            ? [
                {
                  key: "batchAssembly",
                  label: "Собрать задания",
                  icon: <PlaylistAddCheckIcon />,
                  count: batchTaskInfos.length,
                  primary: true,
                  onClick: () => setBatchDialogOpen(true),
                },
              ]
            : []),
          ...(labelOrderIds.length > 0
            ? [{...downloadLabels.getAction(labelOrderIds), count: labelOrderIds.length}]
            : []),
        ]
      : [];

  function renderOrder(order: OrderDetailsDto) {
    return (
      <AssemblyOrderAccordion
        key={order.id}
        order={order}
        canFulfill={canFulfill}
        selectedTaskIds={selectedTaskIds}
        onTaskCheckChange={handleTaskCheckChange}
        eligibilityMap={eligibilityMap}
      />
    );
  }

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <AppBreadcrumbs
          path={[{name: "Операции", link: "/operations"}, {name: "Сборка заказов"}]}
        />
        <PageGenericHeader
          title={"Сборка заказов"}
          refresh={
            <IconButton color="inherit" onClick={handleRefresh} disabled={showLoading}>
              <RefreshIcon />
            </IconButton>
          }
        >
          <SearchWithItemsInput
            text={searchInput}
            onTextChange={setSearchInput}
            itemIds={catalogItemIds}
            onItemIdsChange={setCatalogItemIds}
            sx={{flexGrow: 1}}
          />
        </PageGenericHeader>
        <FiltersBar activeCount={[warehouseId, tagIds.length > 0].filter(Boolean).length}>
          <WarehousesSelect
            value={warehouseId}
            onChange={setWarehouseId}
            sx={{flexBasis: 200}}
            size="small"
            textFieldProps={{label: "Склад"}}
          />
          <DocumentTagsFilter
            kind="order"
            value={tagIds}
            onChange={setTagIds}
            sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
          />
          <TextField
            select
            size="small"
            label="Группировка"
            value={grouping}
            onChange={(e) => setGrouping(parseAssemblyGrouping(e.target.value))}
            sx={{flexBasis: 200}}
          >
            {(Object.keys(ASSEMBLY_GROUPING_LABELS) as AssemblyGrouping[]).map((g) => (
              <MenuItem key={g} value={g}>
                {ASSEMBLY_GROUPING_LABELS[g]}
              </MenuItem>
            ))}
          </TextField>
        </FiltersBar>

        <BulkBar
          count={selectedOrders.length}
          countLabel={{one: "заказ выбран", few: "заказа выбрано", many: "заказов выбрано"}}
          onClear={() => setSelectedTaskIds(new Set())}
          actions={selectionActions}
          info={listStats}
          infoLoading={showLoading}
        />

        {ordersQuery.isError && (
          <Alert severity="error">Не удалось загрузить заказы на сборке</Alert>
        )}

        {showLoading && (
          <Box sx={{display: "flex", justifyContent: "center", p: 4}}>
            <CircularProgress />
          </Box>
        )}

        {orders.length === 0 && !showLoading && (
          <Box sx={{p: 4, textAlign: "center"}}>
            <Typography color="text.secondary">
              {searchString || warehouseId || catalogItemIds.length > 0
                ? "Ничего не найдено"
                : "Нет заказов на сборке"}
            </Typography>
          </Box>
        )}

        {!showLoading && orders.length > 0 && (
          <Stack direction="row" spacing={1} sx={{alignItems: "center", pl: 1}}>
            <Checkbox
              size="small"
              checked={allVisibleSelected}
              indeterminate={visibleSelectedCount > 0 && !allVisibleSelected}
              onChange={handleToggleAllVisible}
              slotProps={{input: {"aria-label": "Выбрать все"}}}
              sx={{p: 0.5}}
            />
            <Typography variant="subtitle2">Выбрать все</Typography>
            <Typography variant="body2" color="text.secondary" sx={{whiteSpace: "nowrap"}}>
              {pluralCount(orders.length, NOUNS.order)}
            </Typography>
          </Stack>
        )}

        {!showLoading &&
          (grouping === "none"
            ? orders.map(renderOrder)
            : groups.map((group) => (
                // The mode prefix remounts groups on a mode switch, so they start collapsed again.
                <AssemblyOrderGroup
                  key={`${grouping}:${group.key}`}
                  label={group.label}
                  orders={group.orders}
                  selectedTaskIds={selectedTaskIds}
                  onTaskCheckChange={handleTaskCheckChange}
                >
                  {group.orders.map(renderOrder)}
                </AssemblyOrderGroup>
              )))}

        <BatchAssemblyDialog
          open={batchDialogOpen}
          onClose={() => {
            setBatchDialogOpen(false);
            setSelectedTaskIds(new Set());
          }}
          selectedTasks={batchTaskInfos}
        />
        {downloadLabels.dialogs}
      </Stack>
    </CatalogItemDrawerHost>
  );
}

export default OrdersAssemblyPage;
