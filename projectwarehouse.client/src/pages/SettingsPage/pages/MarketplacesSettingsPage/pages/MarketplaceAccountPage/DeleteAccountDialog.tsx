import {useEffect} from "react";
import {Typography} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {
  marketplacesDeleteAccountMutation,
  marketplacesGetAccountsQueryKey,
} from "@/api/@tanstack/react-query.gen";
import ConfirmDialog from "@/components/ConfirmDialog";

interface DeleteAccountDialogProps {
  open: boolean;
  accountId: string;
  accountName: string;
  onClose: () => void;
}

function DeleteAccountDialog({open, accountId, accountName, onClose}: DeleteAccountDialogProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    ...marketplacesDeleteAccountMutation(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: marketplacesGetAccountsQueryKey()});
      navigate("/settings/integrations");
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
      title="Отключить магазин?"
      onConfirm={() => mutation.mutate({path: {id: accountId}})}
      isPending={mutation.isPending}
      confirmText="Отключить"
      confirmColor="error"
    >
      <Typography>
        Магазин «{accountName}» будет удалён вместе со складами, карточками, их привязками и
        историей синхронизаций.
      </Typography>
    </ConfirmDialog>
  );
}

export default DeleteAccountDialog;
