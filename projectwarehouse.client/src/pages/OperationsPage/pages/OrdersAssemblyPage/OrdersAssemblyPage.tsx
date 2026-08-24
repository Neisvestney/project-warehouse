import {useCallback, useEffect, useMemo, useState} from "react";
import {Alert, Box, Button, CircularProgress, IconButton, Stack, Typography} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {ordersGetAllAssemblyOptions} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {useEntityWatchMany} from "@/hooks/useEntityWatch";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import {byOperation} from "@/utils/queryKeys";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import AssemblyOrderAccordion from "./AssemblyOrderAccordion";
import {checkBatchEligibility, hasRemainingWork} from "./batchEligibility";
import BatchAssemblyDialog, {type SelectedTaskInfo} from "./BatchAssemblyDialog";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import FiltersBar from "@/components/FiltersBar.tsx";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState.ts";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState.ts";
import SearchInput from "@/components/SearchInput.tsx";
import WarehousesSelect from "@/components/WarehousesSelect.tsx";

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

  const ordersQuery = useQuery({
    ...ordersGetAllAssemblyOptions({
      query: {warehouseId: warehouseId ?? undefined, searchString: searchString || undefined},
    }),
  });

  const orders = useMemo<OrderDetailsDto[]>(() => ordersQuery.data ?? [], [ordersQuery.data]);

  // No edit lock here on purpose: several assemblers on one order is the normal case, and they need
  // to see each other's fulfillments instead of a "being edited" banner.
  const queryClient = useQueryClient();
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

  function handleSelectAllEligible() {
    const eligibleIds = new Set<string>();
    for (const order of orders) {
      for (const task of order.assemblyTasks) {
        if (checkBatchEligibility(task) && hasRemainingWork(task) && task.status !== "done") {
          eligibleIds.add(task.id);
        }
      }
    }
    setSelectedTaskIds(eligibleIds);
  }

  // Counts come from here, not from selectedTaskIds: ids of tasks hidden by the search/warehouse
  // filter stay in the set, and the dialog only ever gets the visible ones.
  const selectedTaskInfos = useMemo<SelectedTaskInfo[]>(() => {
    const result: SelectedTaskInfo[] = [];
    for (const order of orders) {
      for (const task of order.assemblyTasks) {
        if (selectedTaskIds.has(task.id)) {
          result.push({orderId: order.id, taskId: task.id, task, warehouseId: order.warehouseId});
        }
      }
    }
    return result;
  }, [selectedTaskIds, orders]);

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
          actions={
            canFulfill && (
              <>
                <Button size="small" variant="outlined" onClick={handleSelectAllEligible}>
                  Выбрать все доступные для массовой сборки
                </Button>
                {selectedTaskInfos.length >= 1 && (
                  <Button size="small" variant="contained" onClick={() => setBatchDialogOpen(true)}>
                    Собрать выбранные ({selectedTaskInfos.length})
                  </Button>
                )}
              </>
            )
          }
        >
          <SearchInput value={searchInput} onChange={setSearchInput} />
        </PageGenericHeader>
        <FiltersBar>
          <WarehousesSelect
            value={warehouseId}
            onChange={setWarehouseId}
            sx={{flexBasis: 200}}
            size="small"
            textFieldProps={{label: "Склад"}}
          />
        </FiltersBar>

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
              {searchString ? "Ничего не найдено" : "Нет заказов на сборке"}
            </Typography>
          </Box>
        )}

        {!showLoading &&
          orders.map((order) => (
            <AssemblyOrderAccordion
              key={order.id}
              order={order}
              canFulfill={canFulfill}
              selectedTaskIds={selectedTaskIds}
              onTaskCheckChange={handleTaskCheckChange}
              eligibilityMap={eligibilityMap}
            />
          ))}

        {batchDialogOpen && (
          <BatchAssemblyDialog
            open
            onClose={() => {
              setBatchDialogOpen(false);
              setSelectedTaskIds(new Set());
            }}
            selectedTasks={selectedTaskInfos}
          />
        )}
      </Stack>
    </CatalogItemDrawerHost>
  );
}

export default OrdersAssemblyPage;
