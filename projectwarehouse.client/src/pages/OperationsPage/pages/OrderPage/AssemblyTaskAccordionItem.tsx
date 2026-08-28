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
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersDeleteAssemblyTaskMutation,
  ordersGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {AssemblyTaskBoxComponentDto, AssemblyTaskDto, OrderDetailsDto} from "@/api/types.gen";
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
    component: AssemblyTaskBoxComponentDto;
    boxLabel: string | null | undefined;
  } | null>(null);
  const [fulfillmentsDrawerOpen, setFulfillmentsDrawerOpen] = useState(false);

  const {fulfilled: fulfilledComponents, total: totalComponents} = getTaskProgress(task);

  const deleteMutation = useMutation({
    ...ordersDeleteAssemblyTaskMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
      setConfirmDelete(false);
    },
  });

  const canDelete = canEdit && task.status === "pending";

  function openFulfillments(component: AssemblyTaskBoxComponentDto, boxLabel?: string | null) {
    setFulfillmentsTarget({component, boxLabel});
    setFulfillmentsDrawerOpen(true);
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
                      onClick={() => openFulfillments(c, box.orderBoxLabel)}
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
                        onClick={() => openFulfillments(c, box.orderBoxLabel)}
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
        open={fulfillmentsDrawerOpen}
        onClose={() => setFulfillmentsDrawerOpen(false)}
        title={fulfillmentsTarget?.component.catalogItemName ?? ""}
        subtitle={fulfillmentsTarget?.boxLabel ?? undefined}
        quantity={fulfillmentsTarget?.component.quantity ?? 0}
        isVariation={fulfillmentsTarget?.component.catalogItemType === "variation"}
        catalogItemId={fulfillmentsTarget?.component.catalogItemId}
        fulfillments={fulfillmentsTarget?.component.fulfillments ?? []}
      />

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
