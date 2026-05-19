import {useCallback, useState} from "react";
import {useParams, useNavigate} from "react-router";
import {Box, Button, CircularProgress, Stack} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import {useQuery, useMutation, useQueryClient} from "@tanstack/react-query";
import {
  inboundOrdersGetAllQueryKey,
  inboundOrdersGetByIdOptions,
  inboundOrdersDeleteMutation,
} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import type {AppProblemDetails} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ConfirmDialog from "@/components/ConfirmDialog";
import InboundOrderInfoSection from "./components/InboundOrderInfoSection";
import InboundOrderStatusActions from "./components/InboundOrderStatusActions";
import DraftItemsSection from "./components/DraftItemsSection";
import ItemsComparisonSection from "./components/ItemsComparisonSection";

function InboundOrderPage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [statusChangeErrors, setStatusChangeErrors] = useState<AppProblemDetails | null>(null);
  const clearStatusChangeErrors = useCallback(() => setStatusChangeErrors(null), []);
  const [isDraftFormDirty, setIsDraftFormDirty] = useState(false);
  const canEdit = useHasPermission([
    "inbound_orders.edit",
    "inbound_orders.edit_assigned_warehouses",
  ]);

  const {
    data: order,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...inboundOrdersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const [hasProcessedItems, setHasProcessedItems] = useState(false);

  const deleteMutation = useMutation({
    ...inboundOrdersDeleteMutation(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: inboundOrdersGetAllQueryKey()});
      navigate("/inbound-orders");
    },
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!order) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Приходные ордера", link: "/inbound-orders"},
          {name: `Ордер #${order.number}`},
        ]}
      />
      <PageGenericHeader
        title={`Ордер #${order.number}${order.title ? ` — ${order.title}` : ""}`}
        right={
          canEdit ? (
            <Button
              variant="outlined"
              color="error"
              startIcon={<DeleteIcon />}
              onClick={() => setDeleteOpen(true)}
            >
              Удалить
            </Button>
          ) : null
        }
      />

      <InboundOrderInfoSection order={order} />

      <InboundOrderStatusActions
        order={order}
        hasProcessedItems={hasProcessedItems}
        onStatusChangeError={setStatusChangeErrors}
        draftFormDirty={isDraftFormDirty}
      />

      {order.status === "draft" ? (
        <DraftItemsSection
          orderId={order.id}
          externalErrors={statusChangeErrors}
          onExternalErrorsApplied={clearStatusChangeErrors}
          onDirtyChange={setIsDraftFormDirty}
        />
      ) : (
        <ItemsComparisonSection
          orderId={order.id}
          onHasProcessedItemsChange={setHasProcessedItems}
        />
      )}

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        title="Удалить ордер?"
        onConfirm={() => deleteMutation.mutate({path: {id: order.id}})}
        isPending={deleteMutation.isPending}
        confirmText="Удалить"
        confirmColor="error"
      >
        Ордер #{order.number} будет удалён безвозвратно.
      </ConfirmDialog>
    </Stack>
  );
}

export default InboundOrderPage;
