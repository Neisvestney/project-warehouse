import {useState} from "react";
import {useParams} from "react-router";
import {Box, Button, CircularProgress, Paper, Stack, Typography} from "@mui/material";
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
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import OrderStatusChip from "@/components/orders/OrderStatusChip";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import {ORDER_TYPE_LABELS, formatOrderNumber} from "@/components/orders/orderUtils";
import OrderMetaSection from "./OrderMetaSection";
import OrderBoxesSection from "./OrderBoxesSection";
import OrderAssemblyTasksSection from "./OrderAssemblyTasksSection";
import PersonAddIcon from "@mui/icons-material/PersonAdd";
import CheckIcon from "@mui/icons-material/Check";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import UndoIcon from "@mui/icons-material/Undo";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import BlockIcon from "@mui/icons-material/Block";
import {useSnackbar} from "notistack";

function OrderPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  const canEdit = useHasPermission("orders.edit");
  const canSelfAssign = useHasPermission("orders.self_assign");
  const canAssemble = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const [cancelConfirm, setCancelConfirm] = useState(false);

  const query = useQuery({
    ...ordersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const transitionMutation = useMutation({
    ...ordersTransitionStatusMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
      setCancelConfirm(false);
    },
    onError: () => enqueueSnackbar("Ошибка смены статуса", {variant: "error"}),
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

  const actionPending = transitionMutation.isPending || selfAssignMutation.isPending;
  const hasActions =
    (canSelfAssign && order.status === "confirmed") ||
    (canEdit && order.status !== "canceled" && order.status !== "shipped");

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <AppBreadcrumbs
          path={[
            {name: "Операции", link: "/operations"},
            {name: "Заказы"},
            {name: typeLabel, link: `/operations/orders/${order.type}`},
            {name: formatOrderNumber(order.number)},
          ]}
        />

        <PageGenericHeader
          title={
            <Stack direction="row" spacing={1.5} sx={{alignItems: "center", flexWrap: "wrap"}}>
              <Typography variant="h5" component="span">
                {formatOrderNumber(order.number)}
              </Typography>
              <OrderTypeChip type={order.type} />
              <OrderStatusChip status={order.status} />
            </Stack>
          }
          right={
            hasActions ? (
              <Stack direction="row" spacing={1} sx={{flexWrap: "wrap"}}>
                {canSelfAssign && order.status === "confirmed" && (
                  <Button
                    variant="outlined"
                    disabled={actionPending}
                    onClick={() => selfAssignMutation.mutate({path: {id: order.id}})}
                    startIcon={<PersonAddIcon />}
                    loading={selfAssignMutation.isPending}
                  >
                    Взять на себя
                  </Button>
                )}

                {canEdit && order.status === "draft" && (
                  <>
                    <Button
                      variant="contained"
                      disabled={actionPending}
                      onClick={() => transition("confirmed")}
                      startIcon={<CheckIcon />}
                      loading={transitionMutation.isPending}
                    >
                      Подтвердить
                    </Button>
                    <Button
                      color="error"
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => transition("canceled")}
                      startIcon={<BlockIcon />}
                    >
                      Отменить
                    </Button>
                  </>
                )}

                {canEdit && order.status === "confirmed" && (
                  <>
                    <Button
                      variant="contained"
                      disabled={actionPending}
                      onClick={() => transition("assembly")}
                      startIcon={<PlayArrowIcon />}
                      loading={transitionMutation.isPending}
                    >
                      На сборку
                    </Button>
                    <Button
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => transition("draft")}
                      startIcon={<UndoIcon />}
                    >
                      Вернуть в черновик
                    </Button>
                    <Button
                      color="error"
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => transition("canceled")}
                      startIcon={<BlockIcon />}
                    >
                      Отменить
                    </Button>
                  </>
                )}

                {canEdit && order.status === "assembly" && (
                  <>
                    {!hasDoneTasks && (
                      <Button
                        variant="outlined"
                        disabled={actionPending}
                        onClick={() => transition("confirmed")}
                        startIcon={<UndoIcon />}
                        loading={transitionMutation.isPending}
                      >
                        Вернуть в Подтверждён
                      </Button>
                    )}
                    <Button
                      color="error"
                      variant="outlined"
                      disabled={actionPending}
                      onClick={() => transition("canceled")}
                      startIcon={<BlockIcon />}
                    >
                      Отменить
                    </Button>
                  </>
                )}

                {canEdit && order.status === "assembled" && (
                  <Button
                    variant="contained"
                    color="success"
                    disabled={actionPending}
                    onClick={() => transition("shipped")}
                    startIcon={<LocalShippingIcon />}
                    loading={transitionMutation.isPending}
                  >
                    Отгрузить
                  </Button>
                )}
              </Stack>
            ) : undefined
          }
        />

        <Paper>
          <Stack spacing={1.5} sx={{p: 3}}>
            <OrderMetaSection order={order} canEdit={canEdit} />
          </Stack>
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
    </CatalogItemDrawerHost>
  );
}

export default OrderPage;
