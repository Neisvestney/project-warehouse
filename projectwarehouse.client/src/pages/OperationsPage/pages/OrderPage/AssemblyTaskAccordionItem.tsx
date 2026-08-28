import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Chip,
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
        {task.boxes.map((box) => (
          <Box key={box.id}>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{fontWeight: 600, display: "block", px: 2, py: 0.5}}
            >
              {box.orderBoxLabel ?? "Коробка"}
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Позиция</TableCell>
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
                      onClick={() => {
                        setFulfillmentsTarget({component: c, boxLabel: box.orderBoxLabel});
                        setFulfillmentsDrawerOpen(true);
                      }}
                    >
                      <TableCell>
                        <CatalogItemLink catalogItemId={c.catalogItemId} onOpen={openCatalogItem}>
                          <Typography variant="body2">{c.catalogItemName}</Typography>
                        </CatalogItemLink>
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
