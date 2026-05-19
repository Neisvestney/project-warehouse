import {useState} from "react";
import {Button, Stack, Tooltip} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  inboundOrdersChangeStatusToFinishedMutation,
  inboundOrdersChangeStatusToProcessingMutation,
  inboundOrdersGetByIdQueryKey,
  inboundOrdersGetDraftItemsGroupsQueryKey,
  inboundOrdersGetItemsComparisonQueryKey,
  inboundOrdersRollbackStatusToDraftMutation,
  inboundOrdersRollbackStatusToProcessingMutation,
} from "@/api/@tanstack/react-query.gen";
import type {AppProblemDetails, InboundOrderDto} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {useModal} from "@/hooks/useModal";
import {extractErrorMessage, isAppProblemDetails} from "@/utils/errorUtils";
import ConfirmDialog from "@/components/ConfirmDialog";

interface Props {
  order: InboundOrderDto;
  hasProcessedItems: boolean;
  onStatusChangeError?: (error: AppProblemDetails) => void;
  draftFormDirty?: boolean;
}

function InboundOrderStatusActions({
  order,
  hasProcessedItems,
  onStatusChangeError,
  draftFormDirty,
}: Props) {
  const canEdit = useHasPermission([
    "inbound_orders.edit",
    "inbound_orders.edit_assigned_warehouses",
  ]);
  const [rollbackDraftOpen, setRollbackDraftOpen] = useState(false);
  const queryClient = useQueryClient();
  const {showAlert} = useModal();

  const invalidateAll = () => {
    const path = {id: order.id};
    queryClient.removeQueries({queryKey: inboundOrdersGetDraftItemsGroupsQueryKey({path})});
    queryClient.removeQueries({queryKey: inboundOrdersGetItemsComparisonQueryKey({path})});
    return Promise.all([
      queryClient.invalidateQueries({queryKey: inboundOrdersGetByIdQueryKey({path})}),
      queryClient.invalidateQueries({queryKey: inboundOrdersGetDraftItemsGroupsQueryKey({path})}),
      queryClient.invalidateQueries({queryKey: inboundOrdersGetItemsComparisonQueryKey({path})}),
    ]);
  };

  const toProcessingMutation = useMutation({
    ...inboundOrdersChangeStatusToProcessingMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: invalidateAll,
    onError: (error) => {
      if (
        isAppProblemDetails(error) &&
        error.errors?.root?.[0]?.code === "inboundOrderDraftItemsValidationFailed" &&
        onStatusChangeError
      ) {
        onStatusChangeError(error);
      } else {
        showAlert({
          title: "Ошибка",
          message: extractErrorMessage(error),
          severity: "error",
        });
      }
    },
  });

  const rollbackDraftMutation = useMutation({
    ...inboundOrdersRollbackStatusToDraftMutation(),
    onSuccess: async () => {
      await invalidateAll();
      setRollbackDraftOpen(false);
    },
  });

  const toFinishedMutation = useMutation({
    ...inboundOrdersChangeStatusToFinishedMutation(),
    onSuccess: invalidateAll,
  });

  const rollbackProcessingMutation = useMutation({
    ...inboundOrdersRollbackStatusToProcessingMutation(),
    onSuccess: invalidateAll,
  });

  if (!canEdit) return null;

  return (
    <>
      <Stack direction="row" spacing={1}>
        {order.status === "draft" && (
          <Tooltip title={draftFormDirty ? "Сохраните изменения позиций перед сменой статуса" : ""}>
            <span>
              <Button
                variant="contained"
                onClick={() => toProcessingMutation.mutate({path: {id: order.id}})}
                disabled={toProcessingMutation.isPending || !!draftFormDirty}
              >
                Начать обработку
              </Button>
            </span>
          </Tooltip>
        )}

        {order.status === "processing" && (
          <>
            <Button
              variant="contained"
              color="success"
              onClick={() => toFinishedMutation.mutate({path: {id: order.id}})}
              disabled={toFinishedMutation.isPending}
            >
              Завершить
            </Button>
            <Button
              variant="outlined"
              onClick={() => setRollbackDraftOpen(true)}
              disabled={hasProcessedItems}
              title={
                hasProcessedItems
                  ? "Нельзя вернуть в черновик: есть обработанные товары"
                  : undefined
              }
            >
              Вернуть в черновик
            </Button>
          </>
        )}

        {order.status === "finished" && (
          <Button
            variant="outlined"
            onClick={() => rollbackProcessingMutation.mutate({path: {id: order.id}})}
            disabled={rollbackProcessingMutation.isPending}
          >
            Вернуть в обработку
          </Button>
        )}
      </Stack>

      <ConfirmDialog
        open={rollbackDraftOpen}
        onClose={() => setRollbackDraftOpen(false)}
        title="Вернуть в черновик?"
        onConfirm={() => rollbackDraftMutation.mutate({path: {id: order.id}})}
        isPending={rollbackDraftMutation.isPending}
        confirmText="Вернуть"
        confirmColor="warning"
      >
        Заявленные товары будут удалены. Это действие нельзя отменить.
      </ConfirmDialog>
    </>
  );
}

export default InboundOrderStatusActions;
