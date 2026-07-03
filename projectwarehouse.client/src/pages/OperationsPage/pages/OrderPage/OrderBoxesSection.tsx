import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersAddBoxMutation,
  ordersGetByIdQueryKey,
  ordersRemoveBoxMutation,
  ordersUpdateBoxMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrderBoxDto, OrderDetailsDto} from "@/api/types.gen";
import ConfirmDialog from "@/components/ConfirmDialog";
import OrderComponentsTable from "./OrderComponentsTable";

interface OrderBoxesSectionProps {
  order: OrderDetailsDto;
  canEdit: boolean;
}

function OrderBoxesSection({order, canEdit}: OrderBoxesSectionProps) {
  const queryClient = useQueryClient();
  const queryKey = ordersGetByIdQueryKey({path: {id: order.id}});

  const canAddBox =
    canEdit &&
    (order.status === "draft" || order.status === "confirmed" || order.status === "assembly");

  const [newBoxLabel, setNewBoxLabel] = useState("");
  const [addBoxError, setAddBoxError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<OrderBoxDto | null>(null);
  const [editingLabel, setEditingLabel] = useState<{boxId: string; label: string} | null>(null);

  const addBoxMutation = useMutation({
    ...ordersAddBoxMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setNewBoxLabel("");
      setAddBoxError(null);
    },
    onError: () => setAddBoxError("Не удалось добавить коробку"),
  });

  const removeBoxMutation = useMutation({
    ...ordersRemoveBoxMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setDeleteTarget(null);
    },
  });

  const updateBoxMutation = useMutation({
    ...ordersUpdateBoxMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setEditingLabel(null);
    },
  });

  if (order.boxes.length === 1) {
    const box = order.boxes[0];
    return (
      <>
        <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between", mb: 1}}>
          <Typography variant="subtitle2" color="text.secondary">
            Компоненты
          </Typography>
          {canEdit &&
            (order.status === "draft" ||
              order.status === "confirmed" ||
              order.status === "assembly") && (
              <IconButton
                size="small"
                color="error"
                onClick={() => setDeleteTarget(box)}
                title="Удалить коробку"
              >
                <DeleteIcon fontSize="small" />
              </IconButton>
            )}
        </Stack>
        <OrderComponentsTable
          orderId={order.id}
          boxId={box.id}
          components={box.components}
          orderStatus={order.status}
          canEdit={canEdit}
        />
        <Divider sx={{my: 2}} />
        {canAddBox && (
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <TextField
              label="Название новой коробки"
              size="small"
              value={newBoxLabel}
              onChange={(e) => setNewBoxLabel(e.target.value)}
              sx={{flex: 1}}
            />
            <Button
              variant="outlined"
              size="small"
              startIcon={addBoxMutation.isPending ? <CircularProgress size={14} /> : <AddIcon />}
              disabled={addBoxMutation.isPending}
              onClick={() =>
                addBoxMutation.mutate({
                  path: {id: order.id},
                  body: {label: newBoxLabel.trim() || null},
                })
              }
            >
              Добавить коробку
            </Button>
          </Stack>
        )}
        {addBoxError && (
          <Alert severity="error" sx={{mt: 1}}>
            {addBoxError}
          </Alert>
        )}
        <ConfirmDialog
          open={!!deleteTarget}
          onClose={() => setDeleteTarget(null)}
          title="Удалить коробку?"
          confirmText="Удалить"
          confirmColor="error"
          onConfirm={() =>
            deleteTarget && removeBoxMutation.mutate({path: {id: order.id, boxId: deleteTarget.id}})
          }
          isPending={removeBoxMutation.isPending}
        />
      </>
    );
  }

  return (
    <>
      {order.boxes.map((box) => (
        <Accordion key={box.id} defaultExpanded disableGutters>
          <AccordionSummary
            expandIcon={<ExpandMoreIcon />}
            sx={{bgcolor: "grey.50", "&:hover": {bgcolor: "grey.100"}}}
          >
            <Stack
              direction="row"
              sx={{alignItems: "center", justifyContent: "space-between", width: "100%", pr: 1}}
            >
              {editingLabel?.boxId === box.id ? (
                <Stack direction="row" spacing={1} onClick={(e) => e.stopPropagation()}>
                  <TextField
                    size="small"
                    value={editingLabel.label}
                    onChange={(e) => setEditingLabel({boxId: box.id, label: e.target.value})}
                    autoFocus
                  />
                  <Button
                    size="small"
                    onClick={() =>
                      updateBoxMutation.mutate({
                        path: {id: order.id, boxId: box.id},
                        body: {label: editingLabel.label.trim() || null},
                      })
                    }
                    disabled={updateBoxMutation.isPending}
                  >
                    Сохранить
                  </Button>
                  <Button size="small" onClick={() => setEditingLabel(null)}>
                    Отмена
                  </Button>
                </Stack>
              ) : (
                <Typography variant="body2" sx={{fontWeight: 500}}>
                  {box.label ?? "Коробка без названия"}
                  {canEdit && (
                    <Box
                      component="span"
                      sx={{ml: 1, fontSize: 12, color: "text.secondary", cursor: "pointer"}}
                      onClick={(e) => {
                        e.stopPropagation();
                        setEditingLabel({boxId: box.id, label: box.label ?? ""});
                      }}
                    >
                      (изменить)
                    </Box>
                  )}
                </Typography>
              )}
              {canEdit &&
                (order.status === "draft" ||
                  order.status === "confirmed" ||
                  order.status === "assembly") && (
                  <IconButton
                    size="small"
                    color="error"
                    onClick={(e) => {
                      e.stopPropagation();
                      setDeleteTarget(box);
                    }}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                )}
            </Stack>
          </AccordionSummary>
          <AccordionDetails sx={{p: 0}}>
            <OrderComponentsTable
              orderId={order.id}
              boxId={box.id}
              components={box.components}
              orderStatus={order.status}
              canEdit={canEdit}
            />
          </AccordionDetails>
        </Accordion>
      ))}

      {canAddBox && (
        <Stack direction="row" spacing={1} sx={{alignItems: "center", mt: 1}}>
          <TextField
            label="Название новой коробки"
            size="small"
            value={newBoxLabel}
            onChange={(e) => setNewBoxLabel(e.target.value)}
            sx={{flex: 1}}
          />
          <Button
            variant="outlined"
            size="small"
            startIcon={addBoxMutation.isPending ? <CircularProgress size={14} /> : <AddIcon />}
            disabled={addBoxMutation.isPending}
            onClick={() =>
              addBoxMutation.mutate({
                path: {id: order.id},
                body: {label: newBoxLabel.trim() || null},
              })
            }
          >
            Добавить коробку
          </Button>
        </Stack>
      )}
      {addBoxError && (
        <Alert severity="error" sx={{mt: 1}}>
          {addBoxError}
        </Alert>
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title="Удалить коробку?"
        confirmText="Удалить"
        confirmColor="error"
        onConfirm={() =>
          deleteTarget && removeBoxMutation.mutate({path: {id: order.id, boxId: deleteTarget.id}})
        }
        isPending={removeBoxMutation.isPending}
      />
    </>
  );
}

export default OrderBoxesSection;
