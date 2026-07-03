import {useState} from "react";
import {
  Alert,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersCreateAssemblyTaskMutation,
  ordersGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {OrderBoxDto, OrderDetailsDto} from "@/api/types.gen";
import UsersSelect from "@/components/UsersSelect";

interface TaskBox {
  orderBoxId: string;
  selected: boolean;
  components: {catalogItemId: string; name: string; maxQty: number; qty: number}[];
}

function initTaskBoxes(boxes: OrderBoxDto[]): TaskBox[] {
  return boxes.map((box) => ({
    orderBoxId: box.id,
    selected: true,
    components: box.components.map((c) => ({
      catalogItemId: c.catalogItemId,
      name: c.catalogItemName,
      maxQty: c.quantity,
      qty: c.quantity,
    })),
  }));
}

interface CreateAssemblyTaskDialogProps {
  open: boolean;
  onClose: () => void;
  order: OrderDetailsDto;
}

function CreateAssemblyTaskDialog({open, onClose, order}: CreateAssemblyTaskDialogProps) {
  const queryClient = useQueryClient();
  const [assignedToIds, setAssignedToIds] = useState<string[]>([]);
  const [taskBoxes, setTaskBoxes] = useState<TaskBox[]>(() => initTaskBoxes(order.boxes));
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    ...ordersCreateAssemblyTaskMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: order.id}})});
      onClose();
    },
    onError: () => setError("Не удалось создать задание"),
  });

  function toggleBox(boxId: string) {
    setTaskBoxes((prev) =>
      prev.map((tb) => (tb.orderBoxId === boxId ? {...tb, selected: !tb.selected} : tb)),
    );
  }

  function setComponentQty(boxId: string, catalogItemId: string, qty: number) {
    setTaskBoxes((prev) =>
      prev.map((tb) =>
        tb.orderBoxId === boxId
          ? {
              ...tb,
              components: tb.components.map((c) =>
                c.catalogItemId === catalogItemId ? {...c, qty: Math.max(0, qty)} : c,
              ),
            }
          : tb,
      ),
    );
  }

  function handleSubmit() {
    setError(null);
    const selectedBoxes = taskBoxes.filter((tb) => tb.selected);
    if (selectedBoxes.length === 0) {
      setError("Выберите хотя бы одну коробку");
      return;
    }
    mutation.mutate({
      path: {id: order.id},
      body: {
        assignedToId: assignedToIds[0] ?? null,
        boxes: selectedBoxes.map((tb) => ({
          orderBoxId: tb.orderBoxId,
          components: tb.components
            .filter((c) => c.qty > 0)
            .map((c) => ({catalogItemId: c.catalogItemId, quantity: c.qty})),
        })),
      },
    });
  }

  const boxLabels = Object.fromEntries(
    order.boxes.map((b) => [b.id, b.label ?? `Коробка #${b.id.slice(0, 8)}`]),
  );

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Создать задание на сборку</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          <UsersSelect
            value={assignedToIds}
            onChange={(ids) => setAssignedToIds(ids.slice(0, 1))}
            label="Назначить сотрудника"
            size="small"
          />
          <Divider />
          <Typography variant="subtitle2">Состав задания</Typography>
          {taskBoxes.map((tb) => (
            <Stack key={tb.orderBoxId} spacing={0.5}>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={tb.selected}
                    onChange={() => toggleBox(tb.orderBoxId)}
                    size="small"
                  />
                }
                label={
                  <Typography variant="body2" sx={{fontWeight: 500}}>
                    {boxLabels[tb.orderBoxId]}
                  </Typography>
                }
              />
              {tb.selected && (
                <Table size="small" sx={{ml: 3}}>
                  <TableHead>
                    <TableRow>
                      <TableCell>Позиция</TableCell>
                      <TableCell sx={{width: 100}}>Кол-во</TableCell>
                      <TableCell sx={{width: 80, color: "text.secondary"}}>Макс.</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {tb.components.map((c) => (
                      <TableRow key={c.catalogItemId}>
                        <TableCell>{c.name}</TableCell>
                        <TableCell>
                          <TextField
                            type="number"
                            size="small"
                            value={c.qty}
                            onChange={(e) =>
                              setComponentQty(
                                tb.orderBoxId,
                                c.catalogItemId,
                                Number(e.target.value),
                              )
                            }
                            slotProps={{htmlInput: {min: 0, max: c.maxQty, style: {width: 60}}}}
                            variant="outlined"
                          />
                        </TableCell>
                        <TableCell>{c.maxQty}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Stack>
          ))}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Создать"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default CreateAssemblyTaskDialog;
