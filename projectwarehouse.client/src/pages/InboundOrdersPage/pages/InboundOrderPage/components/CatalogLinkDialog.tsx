import {useState} from "react";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  List,
  ListItemButton,
  ListItemText,
  MenuItem,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import {useQuery} from "@tanstack/react-query";
import {catalogGetAllOptions, catalogGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {useDebounce} from "@/hooks/useDebounce";

export interface CatalogLinkDialogItemInfo {
  name: string;
  article: string;
  characteristic?: string;
}

export interface CatalogLinkDialogProps {
  open: boolean;
  onClose: () => void;
  /** Called when user confirms; both null = unlink */
  onConfirm: (catalogItemId: string | null, catalogItemWithCharacteristicId: string | null) => void;
  initialCatalogItemId?: string | null;
  initialCharacteristicId?: string | null;
  itemInfo?: CatalogLinkDialogItemInfo | null;
}

export function CatalogLinkDialog({
  open,
  onClose,
  onConfirm,
  initialCatalogItemId,
  initialCharacteristicId,
  itemInfo,
}: CatalogLinkDialogProps) {
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search, 300);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [selectedCharId, setSelectedCharId] = useState<string | null>(null);
  const [prevOpen, setPrevOpen] = useState(false);

  // Reset internal state when dialog transitions from closed → open
  if (open && !prevOpen) {
    setPrevOpen(true);
    setSearch("");
    setSelectedItemId(initialCatalogItemId ?? null);
    setSelectedCharId(initialCharacteristicId ?? null);
  } else if (!open && prevOpen) {
    setPrevOpen(false);
  }

  const catalogQuery = useQuery({
    ...catalogGetAllOptions({query: {searchString: debouncedSearch || undefined, pageSize: 30}}),
    enabled: open,
  });

  const catalogItemQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: selectedItemId!}}),
    enabled: open && !!selectedItemId,
  });

  const handleItemClick = (id: string) => {
    if (selectedItemId === id) return;
    setSelectedItemId(id);
    setSelectedCharId(null);
  };

  const handleConfirm = () => {
    onConfirm(selectedItemId, selectedCharId);
    onClose();
  };

  const handleUnlink = () => {
    onConfirm(null, null);
    onClose();
  };

  const items = catalogQuery.data?.items ?? [];
  const characteristics = catalogItemQuery.data?.characteristics ?? [];
  const canConfirm = !!selectedItemId;
  const selectedCatalogItem = catalogItemQuery.data ?? null;
  const selectedChar = selectedCharId ? characteristics.find((c) => c.id === selectedCharId) : null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Привязка к каталогу</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          {itemInfo && (
            <Box sx={{display: "flex", alignItems: "center", gap: 1}}>
              <Box
                sx={{
                  flex: 1,
                  minWidth: 0,
                  px: 1.5,
                  py: 1,
                  borderRadius: 1,
                  bgcolor: "action.hover",
                }}
              >
                <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
                  Товар в черновике
                </Typography>
                <Tooltip title={itemInfo.name} placement="top-start">
                  <Typography variant="body2" noWrap sx={{fontWeight: 500}}>
                    {itemInfo.name}
                  </Typography>
                </Tooltip>
                <Typography variant="caption" color="text.secondary" noWrap sx={{display: "block"}}>
                  Арт.: {itemInfo.article}
                  {itemInfo.characteristic ? ` · ${itemInfo.characteristic}` : ""}
                </Typography>
              </Box>

              <ArrowForwardIcon sx={{flexShrink: 0, color: "text.disabled"}} />

              <Box
                sx={{
                  flex: 1,
                  minWidth: 0,
                  px: 1.5,
                  py: 1,
                  borderRadius: 1,
                  bgcolor: "action.hover",
                }}
              >
                <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
                  Товар в каталоге
                </Typography>
                {selectedCatalogItem ? (
                  <>
                    <Tooltip title={selectedCatalogItem.name} placement="top-start">
                      <Typography variant="body2" noWrap sx={{fontWeight: 500}}>
                        {selectedCatalogItem.name}
                      </Typography>
                    </Tooltip>
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      noWrap
                      sx={{display: "block"}}
                    >
                      Арт.: {selectedCatalogItem.article}
                      {selectedChar ? ` · ${selectedChar.characteristic}` : ""}
                    </Typography>
                  </>
                ) : (
                  <Typography variant="body2" color="text.disabled" sx={{fontStyle: "italic"}}>
                    Не выбран
                  </Typography>
                )}
              </Box>
            </Box>
          )}
          <TextField
            label="Поиск товара"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            size="small"
            autoFocus
            fullWidth
          />

          <Box
            sx={{
              border: "1px solid",
              borderColor: "divider",
              borderRadius: 1,
              maxHeight: 260,
              overflowY: "auto",
            }}
          >
            {catalogQuery.isLoading && (
              <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
                <CircularProgress size={24} />
              </Box>
            )}
            {!catalogQuery.isLoading && items.length === 0 && (
              <Typography variant="body2" color="text.secondary" sx={{textAlign: "center", py: 3}}>
                Ничего не найдено
              </Typography>
            )}
            <List dense disablePadding>
              {items.map((item) => (
                <ListItemButton
                  key={item.id}
                  selected={selectedItemId === item.id}
                  onClick={() => handleItemClick(item.id)}
                  divider
                >
                  <ListItemText
                    primary={item.name}
                    secondary={`Артикул: ${item.article}${item.barcode ? ` · ШК: ${item.barcode}` : ""} · Хар-к: ${item.characteristicCount}`}
                  />
                </ListItemButton>
              ))}
            </List>
          </Box>

          {selectedItemId && (
            <FormControl size="small" fullWidth>
              <InputLabel>Характеристика</InputLabel>
              <Select
                value={selectedCharId ?? ""}
                onChange={(e) => setSelectedCharId(e.target.value || null)}
                label="Характеристика"
                disabled={catalogItemQuery.isLoading}
              >
                <MenuItem value="">— не выбрана —</MenuItem>
                {characteristics.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.characteristic}
                    {c.barcode ? ` (ШК: ${c.barcode})` : ""}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleUnlink} color="warning" sx={{mr: "auto"}}>
          Снять привязку
        </Button>
        <Button onClick={onClose}>Отмена</Button>
        <Button variant="contained" onClick={handleConfirm} disabled={!canConfirm}>
          Привязать
        </Button>
      </DialogActions>
    </Dialog>
  );
}
