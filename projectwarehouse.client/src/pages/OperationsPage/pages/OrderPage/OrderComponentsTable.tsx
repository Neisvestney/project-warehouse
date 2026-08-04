import {useState} from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersAddComponentMutation,
  ordersGetByIdQueryKey,
  ordersRemoveComponentMutation,
  ordersUpdateComponentMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrderBoxComponentDto, OrderDetailsDto, OrderStatus} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import ConfirmDialog from "@/components/ConfirmDialog";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import FulfillmentsDrawer from "@/components/orders/FulfillmentsDrawer";
import {
  collectBoxComponentFulfillments,
  countFulfilledQty,
} from "@/components/orders/orderAssemblyUtils";

interface OrderComponentsTableProps {
  order: OrderDetailsDto;
  boxId: string;
  components: OrderBoxComponentDto[];
  orderStatus: OrderStatus;
  canEdit: boolean;
}

function OrderComponentsTable({
  order,
  boxId,
  components,
  orderStatus,
  canEdit,
}: OrderComponentsTableProps) {
  const orderId = order.id;
  const queryClient = useQueryClient();
  const queryKey = ordersGetByIdQueryKey({path: {id: orderId}});

  const [fulfillmentsTarget, setFulfillmentsTarget] = useState<OrderBoxComponentDto | null>(null);
  const [fulfillmentsDrawerOpen, setFulfillmentsDrawerOpen] = useState(false);
  const hasAssembly = order.assemblyTasks.length > 0;

  const canAdd = canEdit && (orderStatus === "draft" || orderStatus === "confirmed");
  const canDelete = canEdit && (orderStatus === "draft" || orderStatus === "confirmed");
  const canEditQuantity = canEdit && (orderStatus === "draft" || orderStatus === "confirmed");

  const [addCatalogItemId, setAddCatalogItemId] = useState<string | null>(null);
  const [addQuantity, setAddQuantity] = useState(1);
  const [addError, setAddError] = useState<string | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<OrderBoxComponentDto | null>(null);

  const addMutation = useMutation({
    ...ordersAddComponentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setAddCatalogItemId(null);
      setAddQuantity(1);
      setAddError(null);
    },
    onError: () => setAddError("Не удалось добавить компонент"),
  });

  const deleteMutation = useMutation({
    ...ordersRemoveComponentMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey});
      setDeleteTarget(null);
    },
  });

  const updateMutation = useMutation({
    ...ordersUpdateComponentMutation(),
    onSuccess: () => queryClient.invalidateQueries({queryKey}),
  });

  function handleAddComponent() {
    if (!addCatalogItemId || addQuantity < 1) return;
    addMutation.mutate({
      path: {id: orderId, boxId},
      body: {catalogItemId: addCatalogItemId, quantity: addQuantity},
    });
  }

  function handleQuantityChange(component: OrderBoxComponentDto, newQty: number) {
    if (newQty < 1) return;
    updateMutation.mutate({
      path: {id: orderId, boxId, cid: component.id},
      body: {catalogItemId: component.catalogItemId, quantity: newQty},
    });
  }

  return (
    <>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Позиция</TableCell>
            <TableCell>Тип</TableCell>
            <TableCell sx={{width: 100}}>Кол-во</TableCell>
            {hasAssembly && <TableCell sx={{width: 90}}>Собрано</TableCell>}
            {canDelete && <TableCell sx={{width: 80}} />}
          </TableRow>
        </TableHead>
        <TableBody>
          {components.map((c) => {
            const fulfilledQty = hasAssembly
              ? countFulfilledQty(collectBoxComponentFulfillments(order, boxId, c.catalogItemId))
              : 0;
            return (
              <TableRow
                key={c.id}
                hover
                sx={{cursor: "pointer"}}
                onClick={() => {
                  setFulfillmentsTarget(c);
                  setFulfillmentsDrawerOpen(true);
                }}
              >
                <TableCell>{c.catalogItemName}</TableCell>
                <TableCell>
                  <CatalogItemTypeChip type={c.catalogItemType} />
                </TableCell>
                <TableCell onClick={(e) => e.stopPropagation()}>
                  {canEditQuantity ? (
                    <ClampedIntegerField
                      size="small"
                      value={c.quantity}
                      onCommit={(qty) => handleQuantityChange(c, qty)}
                      slotProps={{htmlInput: {style: {width: 60}}}}
                      variant="outlined"
                    />
                  ) : (
                    c.quantity
                  )}
                </TableCell>
                {hasAssembly && (
                  <TableCell
                    sx={{color: fulfilledQty >= c.quantity ? "success.main" : "text.secondary"}}
                  >
                    {fulfilledQty}
                  </TableCell>
                )}
                {canDelete && (
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <Tooltip title="Удалить">
                      <IconButton size="small" color="error" onClick={() => setDeleteTarget(c)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                )}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>

      {canAdd && (
        <Box sx={{p: 2, pt: 1}}>
          <Stack spacing={1}>
            <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
              <CatalogItemsSelect
                multiple={false}
                value={addCatalogItemId}
                onChange={setAddCatalogItemId}
                label="Добавить позицию"
                types={["standard", "unit", "variation", "bundle"]}
                size="small"
                sx={{flex: 1}}
              />
              <ClampedIntegerField
                label="Кол-во"
                size="small"
                value={addQuantity}
                onCommit={setAddQuantity}
                slotProps={{htmlInput: {style: {width: 70}}}}
              />
              <Button
                size="small"
                sx={{
                  whiteSpace: "nowrap",
                  alignSelf: "stretch",
                }}
                startIcon={addMutation.isPending ? <CircularProgress size={14} /> : <AddIcon />}
                disabled={!addCatalogItemId || addMutation.isPending}
                onClick={handleAddComponent}
              >
                Добавить
              </Button>
            </Stack>
            {addError && <Alert severity="error">{addError}</Alert>}
          </Stack>
        </Box>
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title="Удалить позицию?"
        confirmText="Удалить"
        confirmColor="error"
        onConfirm={() =>
          deleteTarget && deleteMutation.mutate({path: {id: orderId, boxId, cid: deleteTarget.id}})
        }
        isPending={deleteMutation.isPending}
      />

      <FulfillmentsDrawer
        open={fulfillmentsDrawerOpen}
        onClose={() => setFulfillmentsDrawerOpen(false)}
        title={fulfillmentsTarget?.catalogItemName ?? ""}
        quantity={fulfillmentsTarget?.quantity ?? 0}
        isVariation={fulfillmentsTarget?.catalogItemType === "variation"}
        fulfillments={collectBoxComponentFulfillments(
          order,
          boxId,
          fulfillmentsTarget?.catalogItemId ?? "",
        )}
      />
    </>
  );
}

export default OrderComponentsTable;
