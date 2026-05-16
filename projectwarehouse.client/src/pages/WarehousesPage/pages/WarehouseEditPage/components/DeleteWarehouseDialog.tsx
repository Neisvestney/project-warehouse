import {useEffect} from "react";
import {Typography} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {warehousesDeleteMutation, warehousesGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import ConfirmDialog from "@/components/ConfirmDialog";

interface DeleteWarehouseDialogProps {
  open: boolean;
  warehouseId: string;
  warehouseName: string;
  onClose: () => void;
}

function DeleteWarehouseDialog({
  open,
  warehouseId,
  warehouseName,
  onClose,
}: DeleteWarehouseDialogProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    ...warehousesDeleteMutation(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: warehousesGetAllQueryKey()});
      navigate("/warehouses");
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
      title="Удалить склад?"
      onConfirm={() => mutation.mutate({path: {id: warehouseId}})}
      isPending={mutation.isPending}
      confirmText="Удалить"
      confirmColor="error"
    >
      <Typography>
        Склад «{warehouseName}» и все его места хранения будут удалены безвозвратно.
      </Typography>
    </ConfirmDialog>
  );
}

export default DeleteWarehouseDialog;
