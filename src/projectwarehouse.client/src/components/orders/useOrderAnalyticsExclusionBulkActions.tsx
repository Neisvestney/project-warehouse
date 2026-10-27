import DoNotDisturbOnIcon from "@mui/icons-material/DoNotDisturbOn";
import InsightsIcon from "@mui/icons-material/Insights";
import {useQueryClient} from "@tanstack/react-query";
import {
  ordersBatchSetAnalyticsExclusionMutation,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {OrderSummaryDto} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import {useHasPermission} from "@/hooks/usePermission";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import {byOperation} from "@/utils/queryKeys";

/** Bulk «Исключить из аналитики» / «Вернуть в аналитику» for Direct orders. */
export function useOrderAnalyticsExclusionBulkActions() {
  const queryClient = useQueryClient();
  const canEditOrders = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const canEditAnalytics = useHasPermission("analytics.settings");

  const mutation = useOperationMutation(
    "order.set_analytics_exclusion",
    {
      ...ordersBatchSetAnalyticsExclusionMutation(),
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: () =>
        Promise.all([
          queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
          queryClient.invalidateQueries({queryKey: byOperation("ordersGetById")}),
        ]),
    },
    (variables) => ({
      "order.count": variables.body?.orderIds.length ?? 0,
      "order.excluded": variables.body?.excluded ?? false,
    }),
  );

  function getActions(orders: OrderSummaryDto[]): BulkAction[] {
    if (!canEditOrders || !canEditAnalytics) return [];

    const toExclude = orders.filter((o) => !o.excludedFromAnalytics).map((o) => o.id);
    const toInclude = orders.filter((o) => o.excludedFromAnalytics).map((o) => o.id);
    const pendingFor = (excluded: boolean) =>
      mutation.isPending && mutation.variables?.body?.excluded === excluded;

    return [
      ...(toExclude.length > 0
        ? [
            {
              key: "excludeFromAnalytics",
              label: "Исключить из аналитики",
              icon: <DoNotDisturbOnIcon />,
              count: toExclude.length,
              pending: pendingFor(true),
              disabled: mutation.isPending,
              onClick: () => mutation.mutate({body: {orderIds: toExclude, excluded: true}}),
            },
          ]
        : []),
      ...(toInclude.length > 0
        ? [
            {
              key: "includeInAnalytics",
              label: "Вернуть в аналитику",
              icon: <InsightsIcon />,
              count: toInclude.length,
              pending: pendingFor(false),
              disabled: mutation.isPending,
              onClick: () => mutation.mutate({body: {orderIds: toInclude, excluded: false}}),
            },
          ]
        : []),
    ];
  }

  return {getActions};
}
