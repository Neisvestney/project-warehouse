import {useCallback, useState} from "react";
import {
  Alert,
  Button,
  Chip,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import {useMutation} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {stocktakesSyncNodesMutation} from "@/api/@tanstack/react-query.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {extractErrorMessage} from "@/utils/errorUtils";
import ConfirmDialog from "@/components/ConfirmDialog";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import type {StocktakeDto, StocktakeNodeDto} from "@/api/types.gen";

interface StocktakeNodesSectionProps {
  stocktake: StocktakeDto;
  onUpdated: (updated: StocktakeDto) => void;
  /** Lifted so the page can hold the edit lock while the scope is being changed. */
  onEditingChange?: (isEditing: boolean) => void;
}

function StocktakeNodesSection({
  stocktake,
  onUpdated,
  onEditingChange,
}: StocktakeNodesSectionProps) {
  const {enqueueSnackbar} = useSnackbar();
  const canEdit = useHasPermission(["stocktakes.edit", "stocktakes.edit_assigned"]);
  const [pickerOpen, setPickerOpenState] = useState(false);

  const setPickerOpen = useCallback(
    (value: boolean) => {
      setPickerOpenState(value);
      onEditingChange?.(value);
    },
    [onEditingChange],
  );
  const [nodeToRemove, setNodeToRemove] = useState<StocktakeNodeDto | null>(null);

  const mutation = useMutation({
    ...stocktakesSyncNodesMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdated(data);
      setNodeToRemove(null);
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось изменить список ячеек", {
        variant: "error",
      }),
  });

  const syncTo = (nodeIds: string[]) =>
    mutation.mutate({path: {id: stocktake.id}, body: {nodeIds}});

  const currentIds = stocktake.nodes.map((n) => n.storagePlaceNodeId);

  const handleAdd = (nodeId: string) => {
    setPickerOpen(false);
    if (currentIds.includes(nodeId)) {
      enqueueSnackbar("Эта ячейка уже добавлена", {variant: "info"});
      return;
    }
    syncTo([...currentIds, nodeId]);
  };

  return (
    <Paper>
      <Stack spacing={2} sx={{p: 3}}>
        <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
          <Typography variant="h6">Ячейки</Typography>
          <Chip label={stocktake.nodes.length} size="small" />
          <div style={{flexGrow: 1}} />
          {canEdit && (
            <Button
              size="small"
              startIcon={<AddIcon />}
              onClick={() => setPickerOpen(true)}
              disabled={mutation.isPending}
            >
              Добавить ячейку
            </Button>
          )}
        </Stack>

        {stocktake.nodes.length === 0 ? (
          <Alert severity="info">
            Выберите ячейки, которые нужно пересчитать. После старта в них будет подставлен текущий
            остаток.
          </Alert>
        ) : (
          <List dense disablePadding>
            {stocktake.nodes.map((node) => (
              <ListItem
                key={node.id}
                divider
                secondaryAction={
                  canEdit ? (
                    <IconButton
                      edge="end"
                      size="small"
                      disabled={mutation.isPending}
                      onClick={() => setNodeToRemove(node)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  ) : undefined
                }
              >
                <ListItemText
                  primary={formatStoragePlaceNodeName(node.nodePath)}
                  secondary={
                    node.items.length > 0 ? `Посчитано позиций: ${node.items.length}` : undefined
                  }
                />
              </ListItem>
            ))}
          </List>
        )}
      </Stack>

      <SelectNodeModal
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        warehouseId={stocktake.warehouseId}
        onSelect={(node) => handleAdd(node.nodeId)}
      />

      <ConfirmDialog
        open={nodeToRemove !== null}
        onClose={() => setNodeToRemove(null)}
        title="Убрать ячейку из инвентаризации?"
        onConfirm={() => syncTo(currentIds.filter((id) => id !== nodeToRemove?.storagePlaceNodeId))}
        isPending={mutation.isPending}
        confirmText="Убрать"
        confirmColor="error"
      >
        {nodeToRemove && nodeToRemove.items.length > 0
          ? "Строки подсчёта по этой ячейке будут удалены."
          : "Ячейка перестанет участвовать в инвентаризации."}
      </ConfirmDialog>
    </Paper>
  );
}

export default StocktakeNodesSection;
