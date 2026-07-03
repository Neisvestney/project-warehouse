import {useState} from "react";
import {useParams} from "react-router";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  ordersGetByIdOptions,
  ordersGetByIdQueryKey,
  ordersSelfAssignMutation,
  ordersTransitionStatusMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrderStatus} from "@/api/types.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import OrderStatusChip from "@/components/orders/OrderStatusChip";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import {ORDER_TYPE_LABELS, formatOrderNumber} from "@/components/orders/orderUtils";
import OrderMetaSection from "./OrderMetaSection";
import OrderBoxesSection from "./OrderBoxesSection";
import OrderAssemblyTasksSection from "./OrderAssemblyTasksSection";

function OrderPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();

  const canEdit = useHasPermission("orders.edit");
  const canSelfAssign = useHasPermission("orders.self_assign");
  const canAssemble = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const [cancelConfirm, setCancelConfirm] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  const query = useQuery({
    ...ordersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const transitionMutation = useMutation({
    ...ordersTransitionStatusMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
      setStatusError(null);
      setCancelConfirm(false);
    },
    onError: () => setStatusError("Ошибка смены статуса"),
  });

  const selfAssignMutation = useMutation({
    ...ordersSelfAssignMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
    },
  });

  function transition(targetStatus: OrderStatus) {
    if (targetStatus === "canceled") {
      setCancelConfirm(true);
      return;
    }
    setStatusError(null);
    transitionMutation.mutate({path: {id: id!}, body: {targetStatus}});
  }

  if (query.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", p: 4}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isNotFoundError(query.error)) return <NotFound />;
  if (query.isError) return <QueryError error={query.error} />;
  if (!query.data) return null;

  const order = query.data;
  const typeLabel = ORDER_TYPE_LABELS[order.type];
  const hasDoneTasks = order.assemblyTasks.some((t) => t.status === "done");

  return (
    <Stack spacing={3}>
      <AppBreadcrumbs
        path={[
          {name: "Операции", link: "/operations"},
          {name: "Заказы"},
          {name: typeLabel, link: `/operations/orders/${order.type}`},
          {name: formatOrderNumber(order.number)},
        ]}
      />

      <Paper sx={{p: 3}}>
        {/* Header */}
        <Stack direction="row" spacing={2} sx={{alignItems: "center", flexWrap: "wrap", mb: 2}}>
          <Typography variant="h5" sx={{fontWeight: 700}}>
            {formatOrderNumber(order.number)}
          </Typography>
          <OrderTypeChip type={order.type} />
          <OrderStatusChip status={order.status} />

          <Stack direction="row" spacing={1} sx={{ml: "auto", flexWrap: "wrap"}}>
            {statusError && (
              <Alert severity="error" sx={{py: 0, px: 1, alignSelf: "center"}}>
                {statusError}
              </Alert>
            )}

            {/* Self-assign */}
            {canSelfAssign && order.status === "confirmed" && (
              <Button
                size="small"
                variant="outlined"
                onClick={() => selfAssignMutation.mutate({path: {id: order.id}})}
                disabled={selfAssignMutation.isPending}
              >
                Взять на себя
              </Button>
            )}

            {/* Status transitions */}
            {canEdit && order.status === "draft" && (
              <>
                <Button
                  size="small"
                  variant="contained"
                  color="primary"
                  onClick={() => transition("confirmed")}
                  disabled={transitionMutation.isPending}
                >
                  Подтвердить
                </Button>
                <Button
                  size="small"
                  variant="outlined"
                  color="error"
                  onClick={() => transition("canceled")}
                  disabled={transitionMutation.isPending}
                >
                  Отменить
                </Button>
              </>
            )}

            {canEdit && order.status === "confirmed" && (
              <>
                <Button
                  size="small"
                  variant="contained"
                  onClick={() => transition("assembly")}
                  disabled={transitionMutation.isPending}
                >
                  На сборку
                </Button>
                <Button
                  size="small"
                  variant="outlined"
                  onClick={() => transition("draft")}
                  disabled={transitionMutation.isPending}
                >
                  Вернуть в черновик
                </Button>
                <Button
                  size="small"
                  variant="outlined"
                  color="error"
                  onClick={() => transition("canceled")}
                  disabled={transitionMutation.isPending}
                >
                  Отменить
                </Button>
              </>
            )}

            {canEdit && order.status === "assembly" && (
              <>
                {!hasDoneTasks && (
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => transition("confirmed")}
                    disabled={transitionMutation.isPending}
                  >
                    Вернуть в Подтверждён
                  </Button>
                )}
                <Button
                  size="small"
                  variant="outlined"
                  color="error"
                  onClick={() => transition("canceled")}
                  disabled={transitionMutation.isPending}
                >
                  Отменить
                </Button>
              </>
            )}

            {canEdit && order.status === "assembled" && (
              <Button
                size="small"
                variant="contained"
                color="success"
                onClick={() => transition("shipped")}
                disabled={transitionMutation.isPending}
              >
                Отгрузить
              </Button>
            )}

            {transitionMutation.isPending && (
              <CircularProgress size={20} sx={{alignSelf: "center"}} />
            )}
          </Stack>
        </Stack>

        <Divider sx={{my: 2}} />

        <OrderMetaSection order={order} canEdit={canEdit} />
      </Paper>

      <Paper sx={{p: 3}}>
        <Typography variant="subtitle1" sx={{fontWeight: 600, mb: 2}}>
          Коробки и состав
        </Typography>
        <OrderBoxesSection order={order} canEdit={canEdit} />
      </Paper>

      {(order.status === "assembly" || order.status === "assembled") && (
        <Paper sx={{p: 3}}>
          <OrderAssemblyTasksSection order={order} canEdit={canEdit || canAssemble} />
        </Paper>
      )}

      <ConfirmDialog
        open={cancelConfirm}
        onClose={() => setCancelConfirm(false)}
        title="Отменить заказ?"
        confirmText="Отменить заказ"
        confirmColor="error"
        onConfirm={() =>
          transitionMutation.mutate({path: {id: order.id}, body: {targetStatus: "canceled"}})
        }
        isPending={transitionMutation.isPending}
      />
    </Stack>
  );
}

export default OrderPage;
