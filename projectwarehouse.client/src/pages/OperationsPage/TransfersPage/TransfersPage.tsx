import {useState} from "react";
import {
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import {useMutation} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {transfersExecuteMutation} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import LocationField from "@/components/transfers/LocationField";
import InventoryItemPickerModal from "@/components/inventory/InventoryItemPickerModal";
import type {CatalogItemType} from "@/api/types.gen";
import type {SelectedLocation} from "@/components/transfers/SelectLocationModal";
import type {SelectedInventoryItem} from "@/components/inventory/InventoryItemPickerModal";

function itemToCatalogType(item: SelectedInventoryItem): CatalogItemType {
  if (item.type === "unit") return "unit";
  return "standard";
}

function itemLabel(item: SelectedInventoryItem): string {
  if (item.type === "standard") return `${item.catalogItemName} × ${item.count}`;
  return `${item.catalogItemName} [${item.inventoryNumber}]`;
}

function TransfersPage() {
  const {enqueueSnackbar} = useSnackbar();
  const [fromLocation, setFromLocation] = useState<SelectedLocation | null>(null);
  const [toLocation, setToLocation] = useState<SelectedLocation | null>(null);
  const [selectedItems, setSelectedItems] = useState<SelectedInventoryItem[]>([]);
  const [pickerOpen, setPickerOpen] = useState(false);

  const mutation = useMutation({
    ...transfersExecuteMutation(),
    onSuccess: () => {
      enqueueSnackbar("Перемещение выполнено успешно", {variant: "success"});
      setFromLocation(null);
      setToLocation(null);
      setSelectedItems([]);
    },
  });

  const handleSubmit = () => {
    if (!fromLocation || !toLocation || selectedItems.length === 0) return;

    mutation.mutate({
      body: {
        fromNodeId: fromLocation.nodeId,
        toNodeId: toLocation.nodeId,
        items: selectedItems.map((item) => {
          if (item.type === "standard")
            return {catalogItemId: item.catalogItemId, count: item.count};
          return {unitItemId: item.unitItemId};
        }),
      },
    });
  };

  const removeItem = (index: number) => {
    setSelectedItems((prev) => prev.filter((_, i) => i !== index));
  };

  const handleFromChange = (loc: SelectedLocation) => {
    setFromLocation(loc);
    setSelectedItems([]);
  };

  const canSubmit =
    !!fromLocation && !!toLocation && selectedItems.length > 0 && !mutation.isPending;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Операции", link: "/operations"}, {name: "Перемещения"}]} />
      <PageGenericHeader title="Перемещение товаров" />

      <Paper sx={{p: 3, maxWidth: 640}}>
        <Stack spacing={3}>
          {/* From location */}
          <LocationField label="Откуда" value={fromLocation} onChange={handleFromChange} />

          {/* Items selector */}
          <Box>
            <Stack
              direction="row"
              spacing={1}
              sx={{alignItems: "center", mb: selectedItems.length > 0 ? 1.5 : 0}}
            >
              <Typography variant="caption" color="text.secondary" sx={{flexGrow: 1}}>
                Товары для перемещения
              </Typography>
              <Button
                size="small"
                variant="outlined"
                disabled={!fromLocation}
                onClick={() => setPickerOpen(true)}
              >
                Выбрать товары
              </Button>
            </Stack>

            {selectedItems.length > 0 && (
              <Stack spacing={0.5}>
                {selectedItems.map((item, i) => (
                  <Stack
                    key={i}
                    direction="row"
                    spacing={1}
                    sx={{
                      alignItems: "center",
                      px: 1.5,
                      py: 0.75,
                      borderRadius: 1,
                      bgcolor: "action.hover",
                    }}
                  >
                    <CatalogItemTypeChip type={itemToCatalogType(item)} size="small" />
                    <Typography variant="body2" sx={{flexGrow: 1}}>
                      {itemLabel(item)}
                    </Typography>
                    <IconButton size="small" onClick={() => removeItem(i)}>
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                ))}
              </Stack>
            )}

            {!fromLocation && (
              <Typography variant="body2" color="text.disabled" sx={{fontStyle: "italic"}}>
                Сначала выберите источник
              </Typography>
            )}
          </Box>

          <Divider />

          {/* To location */}
          <LocationField label="Куда" value={toLocation} onChange={setToLocation} />

          {/* Submit */}
          <Stack direction="row" sx={{justifyContent: "flex-end"}}>
            <Button
              variant="contained"
              startIcon={
                mutation.isPending ? (
                  <CircularProgress size={16} color="inherit" />
                ) : (
                  <SwapHorizIcon />
                )
              }
              disabled={!canSubmit}
              onClick={handleSubmit}
            >
              Переместить
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {/* Item picker */}
      {fromLocation && (
        <InventoryItemPickerModal
          open={pickerOpen}
          onClose={() => setPickerOpen(false)}
          nodeId={fromLocation.nodeId}
          onConfirm={(items) => {
            setSelectedItems((prev) => {
              const next = [...prev];
              for (const item of items) {
                const isDup =
                  item.type === "standard"
                    ? next.some(
                        (s) => s.type === "standard" && s.catalogItemId === item.catalogItemId,
                      )
                    : next.some((s) => s.type === "unit" && s.unitItemId === item.unitItemId);
                if (!isDup) next.push(item);
              }
              return next;
            });
          }}
        />
      )}
    </Stack>
  );
}

export default TransfersPage;
