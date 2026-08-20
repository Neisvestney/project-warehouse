import {useCallback, useState} from "react";
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
import {useEditLock} from "@/hooks/useEditLock";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import {CatalogItemDrawerHost} from "@/components/catalog/CatalogItemDrawerHost";
import OrderStatusChip from "@/components/orders/OrderStatusChip";
import OrderTypeChip from "@/components/orders/OrderTypeChip";
import DownloadOrderLabelButton from "@/components/orders/marketplace/DownloadOrderLabelButton";
import {ORDER_TYPE_LABELS, formatBoxLabel, formatOrderNumber} from "@/components/orders/orderUtils";
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
import OrderMarketplaceItemsSection from "@/pages/OperationsPage/pages/OrderPage/OrderMarketplaceItemsSection.tsx";

function OrderPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  const canEdit = useHasPermission("orders.edit");
  const canSelfAssign = useHasPermission("orders.self_assign");
  // The lock follows the server's CanEdit(Order), which admits edit_assigned — unlike `canEdit`, which
  // gates the unscoped editing UI.
  const canLockOrder = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const canAssemble = useHasPermission(
    ["orders.assemble_assigned", "orders.edit", "orders.edit_assigned"],
    "any",
  );

  const [isEditingMeta, setIsEditingMeta] = useState(false);
  const [cancelConfirm, setCancelConfirm] = useState(false);
  const [emptyBoxesConfirm, setEmptyBoxesConfirm] = useState<OrderStatus | null>(null);

  const query = useQuery({
    ...ordersGetByIdOptions({path: {id: id!}}),
    gcTime: 0,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const refreshOrder = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
  }, [queryClient, id]);

  const lock = useEditLock("order", id, {
    isDirty: isEditingMeta,
    dataUpdatedAt: query.dataUpdatedAt,
    onRefresh: refreshOrder,
    enabled: canLockOrder,
  });

  const transitionMutation = useMutation({
    ...ordersTransitionStatusMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
      setCancelConfirm(false);
      setEmptyBoxesConfirm(null);
    },
    onError: () => enqueueSnackbar("Ошибка смены статуса", {variant: "error"}),
  });

  const selfAssignMutation = useMutation({
    ...ordersSelfAssignMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: id!}})});
    },
  });

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

  const emptyBoxes = order.boxes.filter((b) => b.components.length === 0);
  const hasBoxIssues = order.boxes.length === 0 || emptyBoxes.length > 0;

  function doTransition(targetStatus: OrderStatus) {
    transitionMutation.mutate({path: {id: order.id}, body: {targetStatus}});
  }

  // «Подтвердить» и «На сборку» дополнительно переспрашивают, если состав заказа неполный
  function transition(targetStatus: OrderStatus, warnOnEmptyBoxes = false) {
    if (targetStatus === "canceled") {
      setCancelConfirm(true);
      return;
    }
    if (warnOnEmptyBoxes && hasBoxIssues) {
      setEmptyBoxesConfirm(targetStatus);
      return;
    }
    doTransition(targetStatus);
  }

  const actionPending = transitionMutation.isPending || selfAssignMutation.isPending;
  const marketplaceOrder = order.type === "fbs" ? order.marketplaceOrder : null;
  const hasActions =
    (canSelfAssign && order.status === "confirmed") ||
    (canEdit && order.status !== "canceled" && order.status !== "shipped") ||
    marketplaceOrder != null;

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <EditLockBanner heldBy={lock.heldBy} />
        <StaleDataBanner
          isStale={!lock.heldBy && lock.isStale}
          staleBy={lock.staleBy}
          onRefresh={lock.refresh}
          onDismiss={lock.dismissStale}
        />

        <AppBreadcrumbs
          path={[
            {name: "Операции", link: "/operations"},
            {name: "Заказы"},
            {name: typeLabel, link: `/operations/orders/${order.type}`},
            {name: formatOrderNumber(order.number)},
          ]}
          viewersOf={{entityType: "order", entityId: id}}
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
                {marketplaceOrder != null && (
                  <DownloadOrderLabelButton
                    orderId={order.id}
                    marketplaceOrder={marketplaceOrder}
                  />
                )}

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
                      onClick={() => transition("confirmed", true)}
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
                      onClick={() => transition("assembly", true)}
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
            <OrderMetaSection order={order} canEdit={canEdit} onEditingChange={setIsEditingMeta} />
          </Stack>
        </Paper>

        {order.marketplaceItems.length > 0 && (
          <Paper sx={{p: 3}}>
            <Typography variant="subtitle1" sx={{fontWeight: 600, mb: 2}}>
              Состав заказа на маркетплейсе
            </Typography>
            <OrderMarketplaceItemsSection order={order} />
          </Paper>
        )}

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

        <ConfirmDialog
          open={emptyBoxesConfirm !== null}
          onClose={() => setEmptyBoxesConfirm(null)}
          title={
            emptyBoxesConfirm === "assembly" ? "Отправить заказ на сборку?" : "Подтвердить заказ?"
          }
          confirmText={emptyBoxesConfirm === "assembly" ? "На сборку" : "Подтвердить"}
          maxWidth="sm"
          onConfirm={() => emptyBoxesConfirm && doTransition(emptyBoxesConfirm)}
          isPending={transitionMutation.isPending}
          confirmColor={"error"}
        >
          {order.boxes.length === 0 ? (
            <Typography variant="body2">В заказе нет ни одной коробки.</Typography>
          ) : (
            <>
              <Typography variant="body2">В заказе есть коробки без компонентов:</Typography>
              <Box component="ul" sx={{mt: 1, mb: 0, pl: 3}}>
                {emptyBoxes.map((box) => (
                  <Typography key={box.id} component="li" variant="body2">
                    {formatBoxLabel(box, order.boxes)}
                  </Typography>
                ))}
              </Box>
            </>
          )}
          <Typography variant="body2" sx={{mt: 2}}>
            Всё равно продолжить?
          </Typography>
        </ConfirmDialog>
      </Stack>
    </CatalogItemDrawerHost>
  );
}

export default OrderPage;
