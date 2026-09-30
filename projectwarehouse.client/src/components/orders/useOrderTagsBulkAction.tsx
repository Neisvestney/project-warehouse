import {useQueryClient} from "@tanstack/react-query";
import {ordersBatchUpdateTagsMutation, ordersGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import {useBulkTagsAction} from "@/components/tags/useBulkTagsAction";
import {NOUNS} from "@/utils/pluralUtils";
import {byOperation} from "@/utils/queryKeys";
import {formatOrderNumber} from "./orderUtils";

/** Bulk «Теги» action for orders; `dialogs` must be rendered by the caller. */
export function useOrderTagsBulkAction() {
  const queryClient = useQueryClient();

  return useBulkTagsAction({
    kind: "order",
    entity: "order",
    mutation: ordersBatchUpdateTagsMutation(),
    invalidate: () =>
      Promise.all([
        queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
        queryClient.invalidateQueries({queryKey: byOperation("ordersGetById")}),
      ]),
    noun: NOUNS.order,
    notFound: {code: "orderNotFound", numbersArg: "orderNumbers", formatNumber: formatOrderNumber},
  });
}
