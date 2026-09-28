import {useState} from "react";
import LocalOfferIcon from "@mui/icons-material/LocalOffer";
import {useQueryClient} from "@tanstack/react-query";
import {ordersBatchUpdateTagsMutation, ordersGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import type {TagBatchOperation} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import BulkTagsDialog from "@/components/tags/BulkTagsDialog";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage, isAppProblemDetails} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {byOperation} from "@/utils/queryKeys";
import {formatOrderNumber} from "./orderUtils";

/** Names the orders that blocked the batch; `count` also covers orders the user cannot see. */
function describeError(error: unknown): string {
  if (isAppProblemDetails(error)) {
    const args = error.errors.root?.find((e) => e.code === "orderNotFound")?.args;
    const count = Number(args?.count);
    const numbers = args?.orderNumbers;
    if (Number.isFinite(count) && Array.isArray(numbers)) {
      const parts = numbers.map((n) => formatOrderNumber(Number(n)));
      const hidden = count - numbers.length;
      if (hidden > 0) parts.push(`ещё ${pluralCount(hidden, NOUNS.order)}`);
      return `Не найдены или недоступны для редактирования: ${parts.join(", ")}. Теги не изменены.`;
    }
  }
  return extractErrorMessage(error);
}

/** Bulk «Теги» action; `dialogs` must be rendered by the caller. */
export function useOrderTagsBulkAction() {
  const queryClient = useQueryClient();
  // captured on click, so the dialog changes what was selected when it opened
  const [dialogOrderIds, setDialogOrderIds] = useState<string[] | null>(null);
  // keeps the count on screen through the closing animation
  const [shownOrderIds] = useRetainedValue(dialogOrderIds);
  const [error, setError] = useState<string | null>(null);

  const mutation = useOperationMutation(
    "order.update_tags",
    {
      ...ordersBatchUpdateTagsMutation(),
      meta: {suppressGlobalError: true},
      onSuccess: async () => {
        setDialogOrderIds(null);
        await Promise.all([
          queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
          queryClient.invalidateQueries({queryKey: byOperation("ordersGetById")}),
        ]);
      },
      onError: (e) => setError(describeError(e)),
    },
    (variables) => ({
      "order.count": variables.body?.ids.length ?? 0,
      "tag.operation": variables.body?.operation ?? "",
    }),
  );

  function handleConfirm(tagId: string, operation: TagBatchOperation) {
    if (!dialogOrderIds) return;
    setError(null);
    mutation.mutate({body: {ids: dialogOrderIds, tagId, operation}});
  }

  function handleClose() {
    setDialogOrderIds(null);
    setError(null);
  }

  function getAction(orderIds: string[]): BulkAction {
    return {
      key: "updateTags",
      label: "Теги",
      icon: <LocalOfferIcon />,
      count: orderIds.length,
      pending: mutation.isPending,
      disabled: orderIds.length === 0 || mutation.isPending,
      onClick: () => setDialogOrderIds(orderIds),
    };
  }

  const dialogs = (
    <BulkTagsDialog
      kind="order"
      open={dialogOrderIds != null}
      isPending={mutation.isPending}
      summary={`Будет затронуто: ${pluralCount(shownOrderIds?.length ?? 0, NOUNS.order)}.`}
      error={error}
      onClose={handleClose}
      onConfirm={handleConfirm}
    />
  );

  return {getAction, dialogs};
}
