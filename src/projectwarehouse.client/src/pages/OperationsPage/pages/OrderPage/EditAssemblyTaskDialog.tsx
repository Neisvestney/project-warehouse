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
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersGetByIdQueryKey,
  ordersUpdateAssemblyTaskMutation,
} from "@/api/@tanstack/react-query.gen";
import type {AssemblyTaskDto} from "@/api/types.gen";
import UsersSelect from "@/components/UsersSelect";
import {useRetainedValue} from "@/hooks/useRetainedValue";

interface EditAssemblyTaskDialogProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  task: AssemblyTaskDto;
}

function EditAssemblyTaskDialog({open, onClose, orderId, task}: EditAssemblyTaskDialogProps) {
  // The content is unmounted only after the exit animation; that is what resets the picked assignee.
  const [shownOpen, releaseShown] = useRetainedValue(open || null);

  useBackClosable(open, onClose);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShown},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownOpen && <EditAssemblyTaskContent onClose={onClose} orderId={orderId} task={task} />}
    </Dialog>
  );
}

function EditAssemblyTaskContent({
  onClose,
  orderId,
  task,
}: Omit<EditAssemblyTaskDialogProps, "open">) {
  const queryClient = useQueryClient();
  const [assignedToIds, setAssignedToIds] = useState<string[]>(() =>
    task.assignedToId ? [task.assignedToId] : [],
  );
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    ...ordersUpdateAssemblyTaskMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: () => setError("Не удалось обновить задание"),
  });

  function handleSubmit() {
    setError(null);
    mutation.mutate({
      path: {id: orderId, taskId: task.id},
      body: {assignedToId: assignedToIds[0] ?? null},
    });
  }

  return (
    <>
      <DialogTitle>Редактировать задание</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          <UsersSelect
            value={assignedToIds}
            onChange={(ids) => setAssignedToIds(ids.slice(0, 1))}
            label="Назначенный сотрудник"
            size="small"
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </>
  );
}

export default EditAssemblyTaskDialog;
