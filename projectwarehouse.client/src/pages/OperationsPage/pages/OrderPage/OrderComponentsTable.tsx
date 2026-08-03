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
  TextField,
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
import type {OrderBoxComponentDto, OrderStatus} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import ConfirmDialog from "@/components/ConfirmDialog";

interface OrderComponentsTableProps {
  orderId: string;
  boxId: string;
  components: OrderBoxComponentDto[];
  orderStatus: OrderStatus;
  canEdit: boolean;
}

function OrderComponentsTable({
  orderId,
  boxId,
  components,
  orderStatus,
  canEdit,
}: OrderComponentsTableProps) {
  const queryClient = useQueryClient();
  const queryKey = ordersGetByIdQueryKey({path: {id: orderId}});

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
            {canDelete && <TableCell sx={{width: 80}} />}
          </TableRow>
        </TableHead>
        <TableBody>
          {components.map((c) => (
            <TableRow key={c.id}>
              <TableCell>{c.catalogItemName}</TableCell>
              <TableCell>
                <CatalogItemTypeChip type={c.catalogItemType} />
              </TableCell>
              <TableCell>
                {canEditQuantity ? (
                  <TextField
                    type="number"
                    size="small"
                    value={c.quantity}
                    onChange={(e) => handleQuantityChange(c, Number(e.target.value))}
                    slotProps={{htmlInput: {min: 1, style: {width: 60}}}}
                    variant="outlined"
                  />
                ) : (
                  c.quantity
                )}
              </TableCell>
              {canDelete && (
                <TableCell>
                  <Tooltip title="Удалить">
                    <IconButton size="small" color="error" onClick={() => setDeleteTarget(c)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              )}
            </TableRow>
          ))}
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
              <TextField
                type="number"
                label="Кол-во"
                size="small"
                value={addQuantity}
                onChange={(e) => setAddQuantity(Math.max(1, Number(e.target.value)))}
                slotProps={{htmlInput: {min: 1, style: {width: 70}}}}
              />
              <Button
                variant="outlined"
                size="small"
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
    </>
  );
}

export default OrderComponentsTable;
