import {useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import type {OrderSummaryDto} from "@/api/types.gen";
import UsersSelect from "@/components/UsersSelect";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

interface AssignOrdersDialogProps {
  /** Orders to assign; null keeps the dialog closed. */
  orders: Pick<OrderSummaryDto, "id" | "warehouseId">[] | null;
  isPending: boolean;
  error: string | null;
  onClose: () => void;
  onConfirm: (assigneeId: string) => void;
}

function AssignOrdersDialog({
  orders,
  isPending,
  error,
  onClose,
  onConfirm,
}: AssignOrdersDialogProps) {
  const open = orders != null;
  const [shownOrders, releaseShown] = useRetainedValue(orders);

  useBackClosable(open && !isPending, onClose);

  return (
    <Dialog
      open={open}
      onClose={isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShown},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownOrders && (
        <AssignOrdersContent
          count={shownOrders.length}
          warehouseIds={[
            ...new Set(shownOrders.flatMap((o) => (o.warehouseId ? [o.warehouseId] : []))),
          ]}
          isPending={isPending}
          error={error}
          onClose={onClose}
          onConfirm={onConfirm}
        />
      )}
    </Dialog>
  );
}

function AssignOrdersContent({
  count,
  warehouseIds,
  isPending,
  error,
  onClose,
  onConfirm,
}: Omit<AssignOrdersDialogProps, "orders"> & {count: number; warehouseIds: string[]}) {
  const [assigneeIds, setAssigneeIds] = useState<string[]>([]);
  const assigneeId = assigneeIds[0];

  return (
    <>
      <DialogTitle>Назначить на сотрудника</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          <Typography variant="body2">
            Будет назначено: {pluralCount(count, NOUNS.order)}. Каждый заказ перейдёт в сборку с
            одним заданием на все коробки.
          </Typography>
          <UsersSelect
            value={assigneeIds}
            onChange={(ids) => setAssigneeIds(ids.slice(-1))}
            label="Сотрудник"
            size="small"
            disabled={isPending}
            warehouseIds={warehouseIds}
            permission="orders.assemble_assigned"
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          onClick={() => assigneeId && onConfirm(assigneeId)}
          disabled={!assigneeId || isPending}
        >
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Назначить"}
        </Button>
      </DialogActions>
    </>
  );
}

export default AssignOrdersDialog;
