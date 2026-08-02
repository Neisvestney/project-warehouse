import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersRemoveFulfillmentMutation,
  ordersGetAllAssemblyQueryKey,
  ordersGetAllQueryKey,
  ordersGetByIdQueryKey,
  ordersTransitionTaskStatusMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  AssemblyFulfillmentDto,
  AssemblyTaskBoxComponentDto,
  AssemblyTaskDto,
  AssemblyTaskStatus,
} from "@/api/types.gen";
import ConfirmDialog from "@/components/ConfirmDialog";
import {countFulfilledQty, getTaskProgress} from "@/components/orders/orderAssemblyUtils";
import AddFulfillmentDialog from "./AddFulfillmentDialog";

const TASK_STATUS_LABELS: Record<AssemblyTaskStatus, string> = {
  pending: "Ожидает",
  inProgress: "В работе",
  done: "Готово",
};

const TASK_STATUS_COLORS: Record<AssemblyTaskStatus, "default" | "warning" | "success"> = {
  pending: "default",
  inProgress: "warning",
  done: "success",
};

interface FulfillmentItemProps {
  fulfillment: AssemblyFulfillmentDto;
  orderId: string;
  taskId: string;
  taskBoxId: string;
  componentId: string;
  canDelete: boolean;
}

function FulfillmentItem({
  fulfillment,
  orderId,
  taskId,
  taskBoxId,
  componentId,
  canDelete,
}: FulfillmentItemProps) {
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const deleteMutation = useMutation({
    ...ordersRemoveFulfillmentMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      setConfirmOpen(false);
    },
  });

  const label = fulfillment.unitInventoryItemId
    ? `Экз. ${fulfillment.unitInventoryItemId.slice(0, 8)}…`
    : fulfillment.bundleComponents?.length
      ? `Bundle (${fulfillment.bundleComponents.length} комп.)`
      : `× ${fulfillment.quantity}`;

  return (
    <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between", py: 0.5}}>
      <Typography variant="caption">{label}</Typography>
      {canDelete && (
        <>
          <Tooltip title="Убрать фулфилмент">
            <IconButton size="small" color="error" onClick={() => setConfirmOpen(true)}>
              <DeleteIcon sx={{fontSize: 16}} />
            </IconButton>
          </Tooltip>
          <ConfirmDialog
            open={confirmOpen}
            onClose={() => setConfirmOpen(false)}
            title="Удалить фулфилмент?"
            confirmText="Удалить"
            confirmColor="error"
            onConfirm={() =>
              deleteMutation.mutate({
                path: {id: orderId, taskId, tbid: taskBoxId, cid: componentId, fid: fulfillment.id},
              })
            }
            isPending={deleteMutation.isPending}
          />
        </>
      )}
    </Stack>
  );
}

interface ComponentRowProps {
  component: AssemblyTaskBoxComponentDto;
  orderId: string;
  warehouseId: string;
  taskId: string;
  taskBoxId: string;
  canFulfill: boolean;
}

function ComponentRow({
  component,
  orderId,
  warehouseId,
  taskId,
  taskBoxId,
  canFulfill,
}: ComponentRowProps) {
  const [addOpen, setAddOpen] = useState(false);

  const fulfilledQty = countFulfilledQty(component.fulfillments);
  const isDone = fulfilledQty >= component.quantity;

  return (
    <Box sx={{mb: 1}}>
      <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          {isDone ? (
            <CheckCircleIcon color="success" sx={{fontSize: 16}} />
          ) : (
            <Box sx={{width: 16, height: 16, borderRadius: "50%", bgcolor: "grey.300"}} />
          )}
          <Typography variant="body2">{component.catalogItemName}</Typography>
          <Typography variant="caption" color="text.secondary">
            {fulfilledQty}/{component.quantity}
          </Typography>
        </Stack>
        {canFulfill && !isDone && (
          <Button size="small" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>
            Добавить
          </Button>
        )}
      </Stack>

      {component.fulfillments.map((f) => (
        <Box key={f.id} sx={{pl: 3}}>
          <FulfillmentItem
            fulfillment={f}
            orderId={orderId}
            taskId={taskId}
            taskBoxId={taskBoxId}
            componentId={component.id}
            canDelete={canFulfill}
          />
        </Box>
      ))}

      {addOpen && (
        <AddFulfillmentDialog
          open
          onClose={() => setAddOpen(false)}
          orderId={orderId}
          warehouseId={warehouseId}
          taskId={taskId}
          taskBoxId={taskBoxId}
          component={component}
        />
      )}
    </Box>
  );
}

interface AssemblyTaskAccordionProps {
  task: AssemblyTaskDto;
  orderId: string;
  warehouseId: string;
  canFulfill: boolean;
  checked?: boolean;
  onCheckChange?: (checked: boolean) => void;
  batchEligible?: boolean;
  defaultExpanded?: boolean;
}

function AssemblyTaskAccordion({
  task,
  orderId,
  warehouseId,
  canFulfill,
  checked,
  onCheckChange,
  batchEligible,
  defaultExpanded,
}: AssemblyTaskAccordionProps) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const {fulfilled: fulfilledComponents, total: totalComponents} = getTaskProgress(task);

  const transitionMutation = useMutation({
    ...ordersTransitionTaskStatusMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
    },
    onError: () => setError("Не удалось обновить статус задания"),
  });

  function handleTransition(targetStatus: "pending" | "inProgress" | "done") {
    setError(null);
    transitionMutation.mutate({path: {id: orderId, taskId: task.id}, body: {targetStatus}});
  }

  return (
    <Accordion disableGutters defaultExpanded={defaultExpanded}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" sx={{alignItems: "center", gap: 1.5, flex: 1, pr: 1}}>
          {onCheckChange !== undefined && (
            <Tooltip title={!batchEligible ? "Содержит Unit компоненты" : ""}>
              <span>
                <Checkbox
                  size="small"
                  checked={checked}
                  disabled={!batchEligible}
                  onChange={(e) => onCheckChange(e.target.checked)}
                  onClick={(e) => e.stopPropagation()}
                  sx={{p: 0.5}}
                />
              </span>
            </Tooltip>
          )}

          <Chip
            label={TASK_STATUS_LABELS[task.status] ?? task.status}
            color={TASK_STATUS_COLORS[task.status] ?? "default"}
            size="small"
          />

          <Typography variant="body2">{task.assignedToName ?? "Не назначен"}</Typography>

          <Typography variant="caption" color="text.secondary" sx={{ml: "auto"}}>
            {fulfilledComponents}/{totalComponents} позиций
          </Typography>
        </Stack>
      </AccordionSummary>

      <AccordionDetails>
        <Stack spacing={0.5}>
          {error && (
            <Alert severity="error" sx={{mb: 1}}>
              {error}
            </Alert>
          )}

          {task.boxes.map((box) => (
            <Box key={box.id}>
              <Typography variant="caption" color="text.secondary" sx={{fontWeight: 600}}>
                {box.orderBoxLabel ?? "Коробка"}
              </Typography>
              {box.components.map((c) => (
                <ComponentRow
                  key={c.id}
                  component={c}
                  orderId={orderId}
                  warehouseId={warehouseId}
                  taskId={task.id}
                  taskBoxId={box.id}
                  canFulfill={canFulfill && task.status !== "done"}
                />
              ))}
              <Divider sx={{my: 1}} />
            </Box>
          ))}

          {canFulfill && (
            <Stack direction="row" spacing={1}>
              {task.status === "pending" && (
                <Button
                  size="small"
                  variant="outlined"
                  onClick={() => handleTransition("inProgress")}
                  disabled={transitionMutation.isPending}
                >
                  Начать
                </Button>
              )}
              {task.status === "inProgress" && (
                <Button
                  size="small"
                  variant="contained"
                  color="success"
                  onClick={() => handleTransition("done")}
                  disabled={transitionMutation.isPending || fulfilledComponents < totalComponents}
                >
                  {transitionMutation.isPending ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    "Завершить"
                  )}
                </Button>
              )}
            </Stack>
          )}
        </Stack>
      </AccordionDetails>
    </Accordion>
  );
}

export default AssemblyTaskAccordion;
