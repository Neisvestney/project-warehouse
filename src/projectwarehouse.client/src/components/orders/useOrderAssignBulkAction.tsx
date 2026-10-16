import {useState} from "react";
import {Alert, Typography} from "@mui/material";
import PersonAddAlt1Icon from "@mui/icons-material/PersonAddAlt1";
import {useQueryClient} from "@tanstack/react-query";
import {ordersBatchAssignMutation, ordersGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import type {BatchAssignFailedItem} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import AssignOrdersDialog from "./AssignOrdersDialog";
import {formatOrderNumber} from "./orderUtils";

/** Bulk «Назначить на…» action for Confirmed orders; `dialogs` and `alerts` must be rendered by the caller. */
export function useOrderAssignBulkAction(onAssigned: (orderIds: string[]) => void) {
  const queryClient = useQueryClient();
  // captured on click, so the dialog assigns what was selected when it opened
  const [dialogOrderIds, setDialogOrderIds] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [failedItems, setFailedItems] = useState<BatchAssignFailedItem[]>([]);

  const mutation = useOperationMutation(
    "order.assign",
    {
      ...ordersBatchAssignMutation(),
      meta: {suppressGlobalError: true},
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: async (data) => {
        onAssigned(data.assignedOrderIds);
        setFailedItems(data.failedItems);
        setDialogOrderIds(null);
        await queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      },
      onError: (e) => setError(extractErrorMessage(e)),
    },
    (variables) => ({"order.count": variables.body?.orderIds.length ?? 0}),
  );

  function handleConfirm(assigneeId: string) {
    if (!dialogOrderIds) return;
    setError(null);
    setFailedItems([]);
    mutation.mutate({body: {orderIds: dialogOrderIds, assigneeId}});
  }

  function getAction(orderIds: string[]): BulkAction {
    return {
      key: "assign",
      label: "Назначить на…",
      icon: <PersonAddAlt1Icon />,
      count: orderIds.length,
      primary: true,
      pending: mutation.isPending,
      disabled: orderIds.length === 0 || mutation.isPending,
      onClick: () => {
        setError(null);
        setDialogOrderIds(orderIds);
      },
    };
  }

  const dialogs = (
    <AssignOrdersDialog
      orderIds={dialogOrderIds}
      isPending={mutation.isPending}
      error={error}
      onClose={() => setDialogOrderIds(null)}
      onConfirm={handleConfirm}
    />
  );

  const alerts = failedItems.length > 0 && (
    <Alert severity="error" onClose={() => setFailedItems([])}>
      <Typography variant="body2" sx={{mb: 0.5}}>
        Часть заказов не удалось назначить:
      </Typography>
      {failedItems.map((f) => (
        <Typography key={f.orderId} variant="caption" sx={{display: "block"}}>
          • {f.orderNumber != null ? formatOrderNumber(f.orderNumber) : "Заказ"}:{" "}
          {resolveErrorMessage(f.error)}
        </Typography>
      ))}
    </Alert>
  );

  return {getAction, dialogs, alerts};
}
