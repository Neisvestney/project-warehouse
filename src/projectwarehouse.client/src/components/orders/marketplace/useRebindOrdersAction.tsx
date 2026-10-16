import {useState} from "react";
import {Typography} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {ordersBatchRebindMutation, ordersGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import type {BatchRebindResponse, OrderSummaryDto} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import ConfirmDialog from "@/components/ConfirmDialog";
import {extractErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {byOperation} from "@/utils/queryKeys";
import RebindResultDialog from "./RebindResultDialog";

/** Orders the rebind accepts: working FBS orders still waiting for assembly. */
export function isRebindable(order: Pick<OrderSummaryDto, "type" | "status" | "isExternal">) {
  return order.type === "fbs" && !order.isExternal && order.status === "confirmed";
}

/** Bulk «Обновить привязку» action for FBS orders; `dialogs` must be rendered by the caller. */
export function useRebindOrdersAction() {
  const queryClient = useQueryClient();
  // captured on click, so the confirmation rebinds what was selected when it opened
  const [confirmIds, setConfirmIds] = useState<string[] | null>(null);
  const [result, setResult] = useState<BatchRebindResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    ...ordersBatchRebindMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async (data) => {
      setResult(data);
      await Promise.all([
        queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
        queryClient.invalidateQueries({queryKey: byOperation("ordersGetById")}),
        queryClient.invalidateQueries({queryKey: byOperation("ordersGetCompositionPreview")}),
      ]);
    },
    onError: (e) => setError(extractErrorMessage(e)),
    onSettled: () => setConfirmIds(null),
  });

  function getAction(selectedOrders: OrderSummaryDto[]): BulkAction | null {
    const ids = selectedOrders.filter(isRebindable).map((o) => o.id);
    if (ids.length === 0) return null;
    return {
      key: "rebind",
      label: "Обновить привязку",
      icon: <LinkIcon />,
      count: ids.length,
      pending: mutation.isPending,
      disabled: mutation.isPending,
      onClick: () => {
        setError(null);
        setConfirmIds(ids);
      },
    };
  }

  const dialogs = (
    <>
      <ConfirmDialog
        open={confirmIds != null}
        onClose={() => setConfirmIds(null)}
        title="Обновить привязку?"
        confirmText="Обновить"
        isPending={mutation.isPending}
        onConfirm={() => confirmIds && mutation.mutate({body: {orderIds: confirmIds}})}
      >
        <Typography variant="body2" sx={{mb: 1}}>
          Будет затронуто: {pluralCount(confirmIds?.length ?? 0, NOUNS.order)}.
        </Typography>
        <Typography variant="body2">
          Позиции, чья карточка теперь привязана к другому товару каталога, будут заменены в
          коробках на новый товар. Раскладка по коробкам сохранится.
        </Typography>
      </ConfirmDialog>
      <RebindResultDialog
        result={result}
        error={error}
        onClose={() => {
          setResult(null);
          setError(null);
        }}
      />
    </>
  );

  return {getAction, dialogs};
}
