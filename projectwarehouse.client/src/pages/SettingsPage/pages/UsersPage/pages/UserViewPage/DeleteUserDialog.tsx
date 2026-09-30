import {useEffect} from "react";
import {Typography} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {usersDeleteMutation, usersGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import ConfirmDialog from "@/components/ConfirmDialog";

interface DeleteUserDialogProps {
  open: boolean;
  userId: string;
  username: string;
  onClose: () => void;
}

function DeleteUserDialog({open, userId, username, onClose}: DeleteUserDialogProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    ...usersDeleteMutation(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: usersGetAllQueryKey()});
      navigate("/settings/employees");
    },
  });

  const {reset} = mutation;
  useEffect(() => {
    if (!open) reset();
  }, [open, reset]);

  return (
    <ConfirmDialog
      open={open}
      onClose={onClose}
      title="Удалить пользователя?"
      onConfirm={() => mutation.mutate({path: {id: userId}})}
      isPending={mutation.isPending}
      confirmText="Удалить"
      confirmColor="error"
    >
      <Typography>Пользователь «{username}» будет удалён безвозвратно.</Typography>
    </ConfirmDialog>
  );
}

export default DeleteUserDialog;
