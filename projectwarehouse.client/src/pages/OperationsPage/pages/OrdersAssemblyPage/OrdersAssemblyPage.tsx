import {useMemo, useState} from "react";
import {Alert, Box, Button, CircularProgress, IconButton, Stack, Typography} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {ordersGetAllAssemblyOptions} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import AssemblyOrderAccordion from "./AssemblyOrderAccordion";
import AssemblyOrderInline from "./AssemblyOrderInline";
import {checkBatchEligibility, hasRemainingWork} from "./batchEligibility";
import BatchAssemblyDialog, {type SelectedTaskInfo} from "./BatchAssemblyDialog";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import FiltersBar from "@/components/FiltersBar.tsx";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState.ts";
import WarehousesSelect from "@/components/WarehousesSelect.tsx";

function OrdersAssemblyPage() {
  const canFulfill = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const [warehouseId, setWarehouseId] = useSyncedWithQueryState(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const ordersQuery = useQuery({
    ...ordersGetAllAssemblyOptions({
      query: {warehouseId: warehouseId ?? undefined},
    }),
    gcTime: 0,
  });

  const orders = useMemo<OrderDetailsDto[]>(() => ordersQuery.data ?? [], [ordersQuery.data]);

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
          right={
            <>
              <IconButton color="inherit" onClick={handleRefresh} disabled={showLoading}>
                <RefreshIcon />
              </IconButton>
              {canFulfill && (
                <Stack direction="row" spacing={1}>
                  <Button size="small" variant="outlined" onClick={handleSelectAllEligible}>
                    Выбрать все доступные для массовой сборки
                  </Button>
                  {selectedTaskIds.size >= 1 && (
                    <Button
                      size="small"
                      variant="contained"
                      onClick={() => setBatchDialogOpen(true)}
                    >
                      Собрать выбранные ({selectedTaskIds.size})
                    </Button>
                  )}
                </Stack>
              )}
            </>
          }
        />
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
            <Typography color="text.secondary">Нет заказов на сборке</Typography>
          </Box>
        )}

        {!showLoading &&
          orders.map((order) => {
            const tasks = order.assemblyTasks;

            if (tasks.length === 1) {
              const task = tasks[0];
              const eligible = eligibilityMap.get(task.id) ?? checkBatchEligibility(task);
              return (
                <AssemblyOrderInline
                  key={order.id}
                  order={order}
                  task={task}
                  canFulfill={canFulfill}
                  checked={selectedTaskIds.has(task.id)}
                  onCheckChange={(checked) => handleTaskCheckChange(order.id, task.id, checked)}
                  batchEligible={eligible}
                />
              );
            }

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
          })}

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
