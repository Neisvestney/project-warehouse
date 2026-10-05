import {useEffect} from "react";
import {Alert, Typography} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {organizationsDeleteMutation} from "@/api/@tanstack/react-query.gen";
import {byOperation} from "@/utils/queryKeys";
import ConfirmDialog from "@/components/ConfirmDialog";

interface DeleteOrganizationDialogProps {
  open: boolean;
  organizationId: string;
  organizationName: string;
  hasAccounts: boolean;
  onClose: () => void;
}

function DeleteOrganizationDialog({
  open,
  organizationId,
  organizationName,
  hasAccounts,
  onClose,
}: DeleteOrganizationDialogProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    ...organizationsDeleteMutation(),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({queryKey: byOperation("organizationsGetAll")}),
        queryClient.invalidateQueries({queryKey: byOperation("organizationsGetShort")}),
      ]);
      navigate("/organizations");
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
      title="Удалить организацию?"
      onConfirm={() => mutation.mutate({path: {id: organizationId}})}
      isPending={mutation.isPending}
      confirmText="Удалить"
      confirmColor="error"
      confirmDisabled={hasAccounts}
    >
      {hasAccounts ? (
        <Alert severity="warning">
          К организации привязаны аккаунты маркетплейсов. Перепривяжите их, прежде чем удалять.
        </Alert>
      ) : (
        <Typography>Организация «{organizationName}» будет удалена.</Typography>
      )}
    </ConfirmDialog>
  );
}

export default DeleteOrganizationDialog;
