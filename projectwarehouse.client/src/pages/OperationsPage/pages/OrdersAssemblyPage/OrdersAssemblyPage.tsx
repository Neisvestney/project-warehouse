import {useMemo, useState} from "react";
import {Alert, Box, Button, CircularProgress, Stack, Typography} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {ordersGetAllAssemblyOptions} from "@/api/@tanstack/react-query.gen";
import type {OrderDetailsDto} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import AssemblyOrderAccordion from "./AssemblyOrderAccordion";
import AssemblyOrderInline from "./AssemblyOrderInline";
import {checkBatchEligibility, hasRemainingWork} from "./batchEligibility";
import BatchAssemblyDialog, {type SelectedTaskInfo} from "./BatchAssemblyDialog";

function OrdersAssemblyPage() {
  const canFulfill = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const ordersQuery = useQuery(ordersGetAllAssemblyOptions());

  const orders = useMemo<OrderDetailsDto[]>(() => ordersQuery.data ?? [], [ordersQuery.data]);

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

  if (ordersQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", p: 4}}>
        <CircularProgress />
      </Box>
    );
  }

  if (ordersQuery.isError) {
    return <Alert severity="error">Не удалось загрузить заказы на сборке</Alert>;
  }

  if (orders.length === 0) {
    return (
      <Box sx={{p: 4, textAlign: "center"}}>
        <Typography color="text.secondary">Нет заказов на сборке</Typography>
      </Box>
    );
  }

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
          <Typography variant="h6">Сборка заказов</Typography>

          {canFulfill && (
            <Stack direction="row" spacing={1}>
              <Button size="small" variant="outlined" onClick={handleSelectAllEligible}>
                Выбрать все доступные для массовой сборки
              </Button>
              {selectedTaskIds.size >= 1 && (
                <Button size="small" variant="contained" onClick={() => setBatchDialogOpen(true)}>
                  Собрать выбранные ({selectedTaskIds.size})
                </Button>
              )}
            </Stack>
          )}
        </Stack>

        {orders.map((order) => {
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
