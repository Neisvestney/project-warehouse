import {useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Select,
  Stack,
  TextField,
} from "@mui/material";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  ordersGetByIdQueryKey,
  ordersGetMoveTargetsOptions,
  ordersMoveComponentMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrderBoxComponentDto} from "@/api/types.gen";

interface OrderComponentMoveDialogProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  boxId: string;
  component: OrderBoxComponentDto;
  maxQuantity: number;
}

function OrderComponentMoveDialog({
  open,
  onClose,
  orderId,
  boxId,
  component,
  maxQuantity,
}: OrderComponentMoveDialogProps) {
  const queryClient = useQueryClient();
  const [mode, setMode] = useState<"existing" | "new">("existing");
  const [targetBoxId, setTargetBoxId] = useState<string>("");
  const [newBoxLabel, setNewBoxLabel] = useState("");
  const [quantity, setQuantity] = useState(maxQuantity);
  const [error, setError] = useState<string | null>(null);

  const targetsQuery = useQuery({
    ...ordersGetMoveTargetsOptions({
      path: {id: orderId, boxId, cid: component.id},
    }),
    enabled: open,
  });

  const moveMutation = useMutation({
    ...ordersMoveComponentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: () => setError("Ошибка перемещения"),
  });

  function handleSubmit() {
    setError(null);
    if (mode === "existing" && !targetBoxId) {
      setError("Выберите целевую коробку");
      return;
    }
    if (mode === "new" && !newBoxLabel.trim()) {
      setError("Введите название новой коробки");
      return;
    }
    moveMutation.mutate({
      path: {id: orderId, boxId, cid: component.id},
      body: {
        quantity,
        targetBoxId: mode === "existing" ? targetBoxId : null,
        newBoxLabel: mode === "new" ? newBoxLabel.trim() : null,
      },
    });
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Переместить «{component.catalogItemName}»</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          <TextField
            label="Количество"
            type="number"
            value={quantity}
            onChange={(e) =>
              setQuantity(Math.max(1, Math.min(maxQuantity, Number(e.target.value))))
            }
            slotProps={{htmlInput: {min: 1, max: maxQuantity}}}
            size="small"
            fullWidth
          />
          <RadioGroup value={mode} onChange={(e) => setMode(e.target.value as "existing" | "new")}>
            <FormControlLabel
              value="existing"
              control={<Radio size="small" />}
              label="В существующую коробку"
            />
            <FormControlLabel
              value="new"
              control={<Radio size="small" />}
              label="В новую коробку"
            />
          </RadioGroup>
          {mode === "existing" && (
            <FormControl size="small" fullWidth>
              <InputLabel>Целевая коробка</InputLabel>
              <Select
                value={targetBoxId}
                onChange={(e) => setTargetBoxId(e.target.value)}
                label="Целевая коробка"
                disabled={targetsQuery.isLoading}
              >
                {targetsQuery.data?.map((box) => (
                  <MenuItem key={box.id} value={box.id}>
                    {box.label ?? `Коробка #${box.id.slice(0, 8)}`}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          {mode === "new" && (
            <TextField
              label="Название новой коробки"
              value={newBoxLabel}
              onChange={(e) => setNewBoxLabel(e.target.value)}
              size="small"
              fullWidth
            />
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={moveMutation.isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={moveMutation.isPending}>
          {moveMutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Переместить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default OrderComponentMoveDialog;
