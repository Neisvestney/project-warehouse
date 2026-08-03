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
  ordersGetAllAssemblyQueryKey,
  ordersGetByIdQueryKey,
  ordersGetTaskMoveTargetsOptions,
  ordersMoveTaskComponentMutation,
} from "@/api/@tanstack/react-query.gen";
import type {AssemblyTaskBoxComponentDto, OrderBoxDto} from "@/api/types.gen";
import {formatBoxLabel} from "@/components/orders/orderUtils";

interface MoveTaskComponentDialogProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  orderBoxes: OrderBoxDto[];
  taskId: string;
  taskBoxId: string;
  component: AssemblyTaskBoxComponentDto;
  maxQuantity: number;
}

function MoveTaskComponentDialog({
  open,
  onClose,
  orderId,
  orderBoxes,
  taskId,
  taskBoxId,
  component,
  maxQuantity,
}: MoveTaskComponentDialogProps) {
  const queryClient = useQueryClient();
  const [mode, setMode] = useState<"existing" | "new">("existing");
  const [targetBoxId, setTargetBoxId] = useState<string>("");
  const [newBoxLabel, setNewBoxLabel] = useState("");
  const [quantityInput, setQuantityInput] = useState(String(maxQuantity));
  const [error, setError] = useState<string | null>(null);

  const parsedQuantity = Number(quantityInput);
  const quantity =
    quantityInput.trim() !== "" && Number.isFinite(parsedQuantity) ? parsedQuantity : 0;

  const targetsQuery = useQuery({
    ...ordersGetTaskMoveTargetsOptions({
      path: {id: orderId, taskId, tbid: taskBoxId, cid: component.id},
    }),
    enabled: open,
  });

  const moveMutation = useMutation({
    ...ordersMoveTaskComponentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: () => setError("Ошибка перемещения"),
  });

  function handleSubmit() {
    setError(null);
    if (quantity < 1 || quantity > maxQuantity) {
      setError(`Количество должно быть от 1 до ${maxQuantity}`);
      return;
    }
    if (mode === "existing" && !targetBoxId) {
      setError("Выберите целевую коробку");
      return;
    }
    if (mode === "new" && !newBoxLabel.trim()) {
      setError("Введите название новой коробки");
      return;
    }
    moveMutation.mutate({
      path: {id: orderId, taskId, tbid: taskBoxId, cid: component.id},
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
            value={quantityInput}
            onChange={(e) => setQuantityInput(e.target.value)}
            onBlur={() => {
              if (quantityInput.trim() === "") return;
              const clamped = Math.max(1, Math.min(maxQuantity, quantity || 1));
              setQuantityInput(String(clamped));
            }}
            slotProps={{htmlInput: {min: 1, max: maxQuantity}}}
            size="small"
            fullWidth
            helperText={`Доступно для перемещения: ${maxQuantity}`}
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
                    {formatBoxLabel(box, orderBoxes)}
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

export default MoveTaskComponentDialog;
