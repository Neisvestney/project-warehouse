import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Chip,
  Paper,
  Divider,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import UndoIcon from "@mui/icons-material/Undo";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import CheckIcon from "@mui/icons-material/Check";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersDeleteAssemblyTaskMutation,
  ordersGetByIdQueryKey,
  ordersRemoveFulfillmentMutation,
  ordersTransitionTaskStatusMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  AssemblyFulfillmentDto,
  AssemblyTaskDto,
  AssemblyTaskStatus,
  OrderDetailsDto,
} from "@/api/types.gen";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import ConfirmDialog from "@/components/ConfirmDialog";
import FulfillmentsDrawer from "@/components/orders/FulfillmentsDrawer";
import {countFulfilledQty, getTaskProgress} from "@/components/orders/orderAssemblyUtils";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import EditAssemblyTaskDialog from "./EditAssemblyTaskDialog";

const TASK_STATUS_LABELS: Record<string, string> = {
  pending: "Ожидает",
  inProgress: "В работе",
  done: "Готово",
};
const TASK_STATUS_COLORS: Record<string, "default" | "warning" | "success"> = {
  pending: "default",
  inProgress: "warning",
  done: "success",
};

interface AssemblyTaskAccordionItemProps {
  task: AssemblyTaskDto;
  order: OrderDetailsDto;
  canEdit: boolean;
}

function AssemblyTaskAccordionItem({task, order, canEdit}: AssemblyTaskAccordionItemProps) {
  const queryClient = useQueryClient();
  const openCatalogItem = useOpenCatalogItem();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [fulfillmentsTarget, setFulfillmentsTarget] = useState<{
    taskBoxId: string;
    componentId: string;
    boxLabel: string | null | undefined;
  } | null>(null);
  const [fulfillmentsDrawerOpen, setFulfillmentsDrawerOpen] = useState(false);
  const [deleteFulfillmentTarget, setDeleteFulfillmentTarget] =
    useState<AssemblyFulfillmentDto | null>(null);

  const {fulfilled: fulfilledComponents, total: totalComponents} = getTaskProgress(task);

  const deleteMutation = useMutation({
    ...ordersDeleteAssemblyTaskMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
      setConfirmDelete(false);
    },
  });

  const transitionMutation = useMutation({
    ...ordersTransitionTaskStatusMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
    },
  });

  const removeFulfillmentMutation = useMutation({
    ...ordersRemoveFulfillmentMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
      setDeleteFulfillmentTarget(null);
    },
  });

  const canDelete = canEdit && task.status === "pending";

  const rollbackTarget: AssemblyTaskStatus | null =
    task.status === "inProgress" ? "pending" : task.status === "done" ? "inProgress" : null;

  const advanceTarget: AssemblyTaskStatus | null =
    task.status === "pending" ? "inProgress" : task.status === "inProgress" ? "done" : null;
  const advanceBlockedByFulfillment =
    advanceTarget === "done" && fulfilledComponents < totalComponents;

  function changeTaskStatus(targetStatus: AssemblyTaskStatus) {
    transitionMutation.mutate({
      path: {id: order.id, taskId: task.id},
      body: {targetStatus},
    });
  }

  function openFulfillments(taskBoxId: string, componentId: string, boxLabel?: string | null) {
    setFulfillmentsTarget({taskBoxId, componentId, boxLabel});
    setFulfillmentsDrawerOpen(true);
  }

  // Re-derived from the live `task` prop on every render — never a stale snapshot from the
  // moment the drawer was opened, so a fulfillment removed via this same drawer disappears
  // from it immediately once the order query refetches.
  const liveFulfillmentsComponent = fulfillmentsTarget
    ? task.boxes
        .find((b) => b.id === fulfillmentsTarget.taskBoxId)
        ?.components.find((c) => c.id === fulfillmentsTarget.componentId)
    : undefined;

  function confirmRemoveFulfillment() {
    if (!deleteFulfillmentTarget || !fulfillmentsTarget) return;
    removeFulfillmentMutation.mutate({
      path: {
        id: order.id,
        taskId: task.id,
        tbid: fulfillmentsTarget.taskBoxId,
        cid: fulfillmentsTarget.componentId,
        fid: deleteFulfillmentTarget.id,
      },
    });
  }

  return (
    <Accordion disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" sx={{alignItems: "center", gap: 1.5, flex: 1, pr: 1}}>
          <Chip
            label={TASK_STATUS_LABELS[task.status] ?? task.status}
            color={TASK_STATUS_COLORS[task.status] ?? "default"}
            size="small"
          />
          <Typography variant="body2">{task.assignedToName ?? "Не назначен"}</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ml: "auto"}}>
            {fulfilledComponents}/{totalComponents}
          </Typography>

          {canEdit && (advanceTarget || rollbackTarget) && (
            <Stack direction="row" sx={{alignItems: "center", gap: 0.5}}>
              {advanceTarget && (
                <Tooltip
                  title={
                    advanceBlockedByFulfillment
                      ? "Не все компоненты собраны"
                      : advanceTarget === "inProgress"
                        ? "Начать сборку"
                        : "Завершить задание"
                  }
                >
                  <span>
                    <IconButton
                      size="small"
                      color={advanceTarget === "done" ? "success" : "default"}
                      onClick={(e) => {
                        e.stopPropagation();
                        changeTaskStatus(advanceTarget);
                      }}
                      disabled={transitionMutation.isPending || advanceBlockedByFulfillment}
                    >
                      {advanceTarget === "done" ? (
                        <CheckIcon fontSize="small" />
                      ) : (
                        <PlayArrowIcon fontSize="small" />
                      )}
                    </IconButton>
                  </span>
                </Tooltip>
              )}

              {rollbackTarget && (
                <Tooltip
                  title={rollbackTarget === "pending" ? "Вернуть в ожидание" : "Вернуть в работу"}
                >
                  <IconButton
                    size="small"
                    onClick={(e) => {
                      e.stopPropagation();
                      changeTaskStatus(rollbackTarget);
                    }}
                    disabled={transitionMutation.isPending}
                  >
                    <UndoIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </Stack>
          )}

          {canEdit && (
            <Tooltip title="Редактировать назначение">
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation();
                  setEditOpen(true);
                }}
              >
                <EditIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}

          {canDelete && (
            <Tooltip title="Удалить задание">
              <IconButton
                size="small"
                color="error"
                onClick={(e) => {
                  e.stopPropagation();
                  setConfirmDelete(true);
                }}
              >
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      </AccordionSummary>

      <AccordionDetails sx={{p: 0}}>
        {task.boxes.map((box, i) => (
          <Box key={box.id}>
            {task.boxes.length > 1 && (
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{fontWeight: 600, display: "block", px: 2, py: 0.5}}
              >
                {box.orderBoxLabel ?? `Коробка №${i}`}
              </Typography>
            )}
            {isMobile ? (
              <Stack spacing={1} sx={{pb: 1}}>
                {box.components.map((c) => {
                  const qty = countFulfilledQty(c.fulfillments);
                  return (
                    <Paper
                      key={c.id}
                      variant="outlined"
                      sx={{p: 1.5, cursor: "pointer"}}
                      onClick={() => openFulfillments(box.id, c.id, box.orderBoxLabel)}
                    >
                      <Stack spacing={1}>
                        <Box sx={{minWidth: 0}}>
                          <CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItem}>
                            <Typography variant="body2">{c.catalogItemName}</Typography>
                          </CatalogItemLink>
                          <Box sx={{mt: 0.5}}>
                            <CatalogItemTypeChip type={c.catalogItemType} />
                          </Box>
                        </Box>
                        <Stack
                          direction="row"
                          spacing={2}
                          sx={{alignItems: "center", flexWrap: "wrap", rowGap: 1}}
                        >
                          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                            <Typography variant="caption" color="text.secondary">
                              Кол-во
                            </Typography>
                            <Typography variant="body2">{c.quantity}</Typography>
                          </Stack>
                          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                            <Typography variant="caption" color="text.secondary">
                              Собрано
                            </Typography>
                            <Typography
                              variant="body2"
                              sx={{color: qty >= c.quantity ? "success.main" : "text.secondary"}}
                            >
                              {qty}
                            </Typography>
                          </Stack>
                        </Stack>
                      </Stack>
                    </Paper>
                  );
                })}
              </Stack>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Позиция</TableCell>
                    <TableCell>Тип</TableCell>
                    <TableCell sx={{width: 80}}>Кол-во</TableCell>
                    <TableCell sx={{width: 80}}>Собрано</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {box.components.map((c) => {
                    const qty = countFulfilledQty(c.fulfillments);
                    return (
                      <TableRow
                        key={c.id}
                        hover
                        sx={{cursor: "pointer"}}
                        onClick={() => openFulfillments(box.id, c.id, box.orderBoxLabel)}
                      >
                        <TableCell>
                          <CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItem}>
                            <Typography variant="body2">{c.catalogItemName}</Typography>
                          </CatalogItemLink>
                        </TableCell>
                        <TableCell>
                          <CatalogItemTypeChip type={c.catalogItemType} />
                        </TableCell>
                        <TableCell>{c.quantity}</TableCell>
                        <TableCell
                          sx={{color: qty >= c.quantity ? "success.main" : "text.secondary"}}
                        >
                          {qty}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
            <Divider />
          </Box>
        ))}
      </AccordionDetails>

      <ConfirmDialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title="Удалить задание на сборку?"
        confirmText="Удалить"
        confirmColor="error"
        onConfirm={() => deleteMutation.mutate({path: {id: order.id, taskId: task.id}})}
        isPending={deleteMutation.isPending}
      />

      <FulfillmentsDrawer
        open={fulfillmentsDrawerOpen && liveFulfillmentsComponent != null}
        onClose={() => setFulfillmentsDrawerOpen(false)}
        title={liveFulfillmentsComponent?.catalogItemName ?? ""}
        subtitle={fulfillmentsTarget?.boxLabel ?? undefined}
        quantity={liveFulfillmentsComponent?.quantity ?? 0}
        isVariation={liveFulfillmentsComponent?.catalogItemType === "variation"}
        catalogItemId={liveFulfillmentsComponent?.catalogItemId}
        fulfillments={liveFulfillmentsComponent?.fulfillments ?? []}
        canDelete={canEdit}
        deletingFulfillmentId={
          removeFulfillmentMutation.isPending ? deleteFulfillmentTarget?.id : undefined
        }
        onRequestDeleteFulfillment={setDeleteFulfillmentTarget}
      />

      <ConfirmDialog
        open={deleteFulfillmentTarget !== null}
        onClose={() => setDeleteFulfillmentTarget(null)}
        title="Отменить фулфилмент?"
        confirmText="Отменить"
        confirmColor="error"
        onConfirm={confirmRemoveFulfillment}
        isPending={removeFulfillmentMutation.isPending}
      >
        <Typography variant="body2">
          Собранный товар вернётся на склад. Действие нельзя отменить.
        </Typography>
      </ConfirmDialog>

      <EditAssemblyTaskDialog
        open={editOpen}
        onClose={() => setEditOpen(false)}
        orderId={order.id}
        task={task}
      />
    </Accordion>
  );
}

export default AssemblyTaskAccordionItem;
