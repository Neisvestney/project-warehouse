import {useState, useMemo, useCallback} from "react";
import {
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Collapse,
  Divider,
  Fab,
  FormControlLabel,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import PrintIcon from "@mui/icons-material/Print";
import SearchIcon from "@mui/icons-material/Search";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  catalogGetAllOptions,
  receiptsDeletePlacementMutation,
  receiptsGetByIdOptions,
  receiptsQuickAddItemMutation,
  receiptsUpdateReceivedCountMutation,
} from "@/api/@tanstack/react-query.gen";
import {useDebounce} from "@/hooks/useDebounce";
import {extractErrorMessage} from "@/utils/errorUtils";
import {useHasPermission} from "@/hooks/usePermission";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import ReceiptItemsEditorDrawer from "@/components/receipts/ReceiptItemsEditorDrawer";
import NotesTableCell from "@/components/NotesTableCell";
import AddPlacementDialog from "@/components/receipts/AddPlacementDialog";
import BatchStandardPlacementDialog from "@/components/receipts/BatchStandardPlacementDialog";
import type {ReceiptDto, ReceiptItemDto, ReceiptItemPlacementDto} from "@/api/types.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {calcTotalPlaced} from "@/components/receipts/receiptUtils";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {openReceiptPrintPage} from "@/utils/printUtils";
import QrCode2Icon from "@mui/icons-material/QrCode2";
import CloseIcon from "@mui/icons-material/Close";
import SelectionTableCell from "@/components/SelectionTableCell";
import {useCatalogLabelsPrintAction} from "@/components/catalog/useCatalogLabelsPrintAction";
import {useSelectedItems} from "@/hooks/useSelectedItems";

const VIRTUAL_TYPES = new Set(["productGroup", "variation", "bundle"]);

const getReceiptItemId = (item: ReceiptItemDto) => item.id;

function selectedCardSx(selected: boolean) {
  return {
    p: 1.5,
    outline: selected ? "2px solid" : undefined,
    outlineColor: selected ? "primary.main" : undefined,
  };
}

function CardCheckbox({checked, onCheck}: {checked: boolean; onCheck: () => void}) {
  return <Checkbox size="small" checked={checked} onChange={onCheck} sx={{p: 0}} />;
}

function CatalogItemCell({item, onOpen}: {item: ReceiptItemDto; onOpen: (id: string) => void}) {
  return (
    <CatalogItemLink catalogItemId={item.catalogItemId} onOpen={onOpen}>
      <CatalogItemTypeChip type={item.catalogItem.type} />
      <Typography variant="body2">{item.catalogItem.fullName}</Typography>
    </CatalogItemLink>
  );
}

function DiscrepancyCell({
  planned,
  received,
}: {
  planned: number;
  received: number | null | undefined;
}) {
  if (received === null || received === undefined) {
    return <TableCell align="right">—</TableCell>;
  }
  const diff = received - planned;
  const color = diff === 0 ? "success.main" : diff < 0 ? "warning.main" : "info.main";
  const label = diff > 0 ? `+${diff}` : String(diff);
  return (
    <TableCell align="right">
      <Typography variant="body2" sx={{color, fontWeight: 500}}>
        {label}
      </Typography>
    </TableCell>
  );
}

function DiscrepancyText({
  planned,
  received,
}: {
  planned: number;
  received: number | null | undefined;
}) {
  if (received === null || received === undefined) return <span>—</span>;
  const diff = received - planned;
  const color = diff === 0 ? "success.main" : diff < 0 ? "warning.main" : "info.main";
  const label = diff > 0 ? `+${diff}` : String(diff);
  return (
    <Typography variant="body2" component="span" sx={{color, fontWeight: 500}}>
      {label}
    </Typography>
  );
}

interface ReceiptItemsSectionProps {
  receipt: ReceiptDto;
  onUpdate: (updated: ReceiptDto) => void;
  /** Lifted so the page can hold the edit lock while the items editor is open. */
  onEditingChange?: (isEditing: boolean) => void;
}

function PlacementDisplay({placement}: {placement: ReceiptItemPlacementDto}) {
  const path = formatStoragePlaceNodeName(placement.nodePath);
  if (placement.inventoryNumber) {
    return (
      <Typography variant="body2">
        {path} — инв. {placement.inventoryNumber}
      </Typography>
    );
  }
  return (
    <Typography variant="body2">
      {path} — {placement.count} шт.
    </Typography>
  );
}

function ReceivedCountInput({
  item,
  receiptId,
  onUpdateItem,
}: {
  item: ReceiptItemDto;
  receiptId: string;
  onUpdateItem: (data: ReceiptItemDto) => void;
}) {
  const {enqueueSnackbar} = useSnackbar();
  const queryKey = receiptsGetByIdOptions({path: {id: receiptId}}).queryKey;
  const queryClient = useQueryClient();
  // The field keeps its typed text after commit; remounting on error brings back the saved value.
  const [resetKey, setResetKey] = useState(0);

  const mutation = useMutation({
    ...receiptsUpdateReceivedCountMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      queryClient.invalidateQueries({queryKey});
      onUpdateItem(data);
    },
    onError: (err) => {
      enqueueSnackbar(extractErrorMessage(err), {variant: "error"});
      setResetKey((k) => k + 1);
    },
  });

  return (
    <ClampedIntegerField
      key={resetKey}
      nullable
      value={item.receivedCount ?? null}
      min={0}
      onCommit={(receivedCount) =>
        mutation.mutate({path: {id: receiptId, itemId: item.id}, body: {receivedCount}})
      }
      onKeyDown={(e) => {
        if (e.key === "Enter") (e.target as HTMLInputElement).blur();
      }}
      size="small"
      sx={{width: 80}}
      disabled={mutation.isPending}
      placeholder="—"
    />
  );
}

function ProcessingItemRow({
  item,
  receipt,
  onUpdate,
  onOpenCatalog,
  selected,
  onToggleSelect,
}: {
  item: ReceiptItemDto;
  receipt: ReceiptDto;
  onUpdate: (data: ReceiptDto) => void;
  onOpenCatalog: (id: string) => void;
  selected: boolean;
  onToggleSelect: (item: ReceiptItemDto, extendRange?: boolean) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [placementDialogOpen, setPlacementDialogOpen] = useState(false);
  const canProcess = useHasPermission("receipts.process_assigned");

  const mergeItem = (updatedItem: ReceiptItemDto): ReceiptDto => ({
    ...receipt,
    items: receipt.items.map((i) => (i.id === updatedItem.id ? updatedItem : i)),
  });

  const deleteMutation = useMutation({
    ...receiptsDeletePlacementMutation(),
    onSuccess: (data) => onUpdate(mergeItem(data)),
  });

  const totalPlaced = useMemo(() => calcTotalPlaced(item), [item]);
  const isVirtual = VIRTUAL_TYPES.has(item.catalogItem.type);

  return (
    <>
      <TableRow hover selected={selected}>
        <SelectionTableCell
          checked={selected}
          onCheck={(extendRange) => onToggleSelect(item, extendRange)}
        />
        <TableCell padding="checkbox">
          <IconButton size="small" onClick={() => setExpanded((v) => !v)}>
            {expanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
        <TableCell>
          <CatalogItemCell item={item} onOpen={onOpenCatalog} />
        </TableCell>
        <TableCell align="right">{item.plannedCount}</TableCell>
        <TableCell>
          <ReceivedCountInput
            item={item}
            receiptId={receipt.id}
            onUpdateItem={(d) => onUpdate(mergeItem(d))}
          />
        </TableCell>
        <DiscrepancyCell planned={item.plannedCount} received={item.receivedCount} />
        <TableCell align="right">
          {totalPlaced > 0 ? (
            <Chip label={totalPlaced} size="small" color="primary" variant="outlined" />
          ) : (
            "—"
          )}
        </TableCell>
        <TableCell>
          {canProcess && !isVirtual && (
            <Button
              startIcon={<AddIcon />}
              size="small"
              onClick={() => setPlacementDialogOpen(true)}
            >
              Разместить
            </Button>
          )}
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={8} sx={{pb: 0, pt: 0}}>
          <Collapse in={expanded} unmountOnExit>
            <Box sx={{py: 1, pl: 6}}>
              {item.placements.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{py: 1}}>
                  Нет размещений
                </Typography>
              ) : (
                <Stack spacing={0.5}>
                  {item.placements.map((placement) => (
                    <Stack key={placement.id} direction="row" sx={{alignItems: "center"}}>
                      <Box sx={{flexGrow: 1}}>
                        <PlacementDisplay placement={placement} />
                      </Box>
                      {canProcess && (
                        <Tooltip title="Удалить размещение">
                          <IconButton
                            size="small"
                            color="error"
                            disabled={deleteMutation.isPending}
                            onClick={() =>
                              deleteMutation.mutate({
                                path: {id: receipt.id, itemId: item.id, placementId: placement.id},
                              })
                            }
                          >
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Stack>
                  ))}
                </Stack>
              )}
            </Box>
          </Collapse>
        </TableCell>
      </TableRow>
      <AddPlacementDialog
        open={placementDialogOpen}
        onClose={() => setPlacementDialogOpen(false)}
        receiptId={receipt.id}
        item={item}
        warehouseId={receipt.warehouseId}
        onUpdate={(updatedItem) => {
          onUpdate(mergeItem(updatedItem));
          setPlacementDialogOpen(false);
        }}
      />
    </>
  );
}

function ProcessingItemCard({
  item,
  receipt,
  onUpdate,
  onOpenCatalog,
  selected,
  onToggleSelect,
}: {
  item: ReceiptItemDto;
  receipt: ReceiptDto;
  onUpdate: (data: ReceiptDto) => void;
  onOpenCatalog: (id: string) => void;
  selected: boolean;
  onToggleSelect: (item: ReceiptItemDto, extendRange?: boolean) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [placementDialogOpen, setPlacementDialogOpen] = useState(false);
  const canProcess = useHasPermission("receipts.process_assigned");

  const {enqueueSnackbar} = useSnackbar();

  const mergeItem = (updatedItem: ReceiptItemDto): ReceiptDto => ({
    ...receipt,
    items: receipt.items.map((i) => (i.id === updatedItem.id ? updatedItem : i)),
  });

  const deleteMutation = useMutation({
    ...receiptsDeletePlacementMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => onUpdate(mergeItem(data)),
    onError: (err) => enqueueSnackbar(extractErrorMessage(err), {variant: "error"}),
  });

  const totalPlaced = useMemo(() => calcTotalPlaced(item), [item]);
  const isVirtual = VIRTUAL_TYPES.has(item.catalogItem.type);

  return (
    <>
      <Paper variant="outlined" sx={selectedCardSx(selected)}>
        <Stack spacing={1}>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <CardCheckbox checked={selected} onCheck={() => onToggleSelect(item)} />
            <CatalogItemCell item={item} onOpen={onOpenCatalog} />
          </Stack>
          <Typography variant="body2" color="text.secondary">
            Запланировано: {item.plannedCount}
          </Typography>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <Typography variant="body2" color="text.secondary">
              Принято:
            </Typography>
            <ReceivedCountInput
              item={item}
              receiptId={receipt.id}
              onUpdateItem={(d) => onUpdate(mergeItem(d))}
            />
          </Stack>
          <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
            <Typography variant="body2" color="text.secondary">
              Расх.:
            </Typography>
            <DiscrepancyText planned={item.plannedCount} received={item.receivedCount} />
          </Stack>
          {totalPlaced > 0 && (
            <Typography variant="body2" color="text.secondary">
              Размещено:{" "}
              <Chip label={totalPlaced} size="small" color="primary" variant="outlined" />
            </Typography>
          )}
          <Stack direction="row" spacing={1} sx={{flexWrap: "wrap"}}>
            {canProcess && !isVirtual && (
              <Button
                startIcon={<AddIcon />}
                size="small"
                onClick={() => setPlacementDialogOpen(true)}
              >
                Разместить
              </Button>
            )}
            <Button
              size="small"
              endIcon={expanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
              onClick={() => setExpanded((v) => !v)}
            >
              Размещения
            </Button>
          </Stack>
          <Collapse in={expanded} unmountOnExit>
            <Divider sx={{mb: 1}} />
            {item.placements.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                Нет размещений
              </Typography>
            ) : (
              <Stack spacing={0.5}>
                {item.placements.map((placement) => (
                  <Stack key={placement.id} direction="row" sx={{alignItems: "center"}}>
                    <Box sx={{flexGrow: 1}}>
                      <PlacementDisplay placement={placement} />
                    </Box>
                    {canProcess && (
                      <Tooltip title="Удалить размещение">
                        <IconButton
                          size="small"
                          color="error"
                          disabled={deleteMutation.isPending}
                          onClick={() =>
                            deleteMutation.mutate({
                              path: {id: receipt.id, itemId: item.id, placementId: placement.id},
                            })
                          }
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </Stack>
                ))}
              </Stack>
            )}
          </Collapse>
        </Stack>
      </Paper>
      <AddPlacementDialog
        open={placementDialogOpen}
        onClose={() => setPlacementDialogOpen(false)}
        receiptId={receipt.id}
        item={item}
        warehouseId={receipt.warehouseId}
        onUpdate={(updatedItem) => {
          onUpdate(mergeItem(updatedItem));
          setPlacementDialogOpen(false);
        }}
      />
    </>
  );
}

const PLACEABLE_TYPES: Array<"standard" | "unit"> = ["standard", "unit"];

function ReceiptItemsSection({receipt, onUpdate, onEditingChange}: ReceiptItemsSectionProps) {
  const [editorOpen, setEditorOpenState] = useState(false);

  const setEditorOpen = useCallback(
    (value: boolean) => {
      setEditorOpenState(value);
      onEditingChange?.(value);
    },
    [onEditingChange],
  );
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);
  const [batchDialogOpen, setBatchDialogOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const canEdit = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);
  const canProcess = useHasPermission("receipts.process_assigned");
  const {status, items} = receipt;

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("lg"));

  const isDraftOrPlanned = status === "draft" || status === "planned";
  const isProcessing = status === "processing";
  const isReadOnly = status === "finished" || status === "canceled";

  const debouncedSearch = useDebounce(searchQuery, 300);
  const trimmedSearch = debouncedSearch.trim();
  const isSearchActive = isProcessing && trimmedSearch.length > 0;

  const catalogSearchQuery = useQuery({
    ...catalogGetAllOptions({
      query: {
        searchString: trimmedSearch,
        itemTypes: PLACEABLE_TYPES,
        pageSize: 50,
        isArchived: false,
      },
    }),
    enabled: isSearchActive,
    meta: {suppressGlobalError: true},
  });

  const receiptCatalogIds = useMemo(() => new Set(items.map((i) => i.catalogItemId)), [items]);

  const visibleItems = useMemo(() => {
    if (!isSearchActive) return items;
    if (!catalogSearchQuery.data) return [];
    const foundIds = new Set(catalogSearchQuery.data.items.map((c) => c.id));
    return items.filter((i) => foundIds.has(i.catalogItemId));
  }, [isSearchActive, catalogSearchQuery.data, items]);

  const extraCatalogItems = useMemo(() => {
    if (!isSearchActive || !catalogSearchQuery.data) return [];
    return catalogSearchQuery.data.items.filter((c) => !receiptCatalogIds.has(c.id));
  }, [isSearchActive, catalogSearchQuery.data, receiptCatalogIds]);

  const [quickAddPendingIds, setQuickAddPendingIds] = useState<Set<string>>(new Set());

  const quickAddMutation = useMutation({
    ...receiptsQuickAddItemMutation(),
    onMutate: ({body}) => {
      setQuickAddPendingIds((prev) => new Set([...prev, body.catalogItemId]));
    },
    onSettled: (_data, _err, {body}) => {
      setQuickAddPendingIds((prev) => {
        const next = new Set(prev);
        next.delete(body.catalogItemId);
        return next;
      });
    },
    onSuccess: onUpdate,
  });

  const {isSelected, allPageSelected, somePageSelected, toggle, toggleAll, clear} =
    useSelectedItems(getReceiptItemId, visibleItems);
  // the hook refreshes only rows on the visible list; search-hidden ones would keep a stale snapshot
  const selectedItems = items.filter((i) => isSelected(i.id));
  const selectedStandardItems = selectedItems.filter((i) => i.catalogItem.type === "standard");
  const canPlaceSelected = isProcessing && canProcess && selectedStandardItems.length > 0;

  const labelsAction = useCatalogLabelsPrintAction({
    countModeLabel: isDraftOrPlanned ? "По запланированному количеству" : "По принятому количеству",
  });

  // with nothing selected the print buttons act on the whole receipt
  const printedItems = selectedItems.length > 0 ? selectedItems : items;
  const selectedSuffix = selectedItems.length > 0 ? ` (${selectedItems.length})` : "";

  return (
    <Box>
      <Stack direction="row" sx={{alignItems: "center", mb: 1, gap: 1, flexWrap: "wrap"}}>
        <Typography variant="h6" sx={{flexGrow: 1}}>
          Позиции
        </Typography>
        {canPlaceSelected && !isMobile && (
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={() => setBatchDialogOpen(true)}
          >
            Разместить ({selectedStandardItems.length})
          </Button>
        )}
        {items.length > 0 && (
          <>
            <Button
              startIcon={<PrintIcon />}
              size="small"
              onClick={() =>
                openReceiptPrintPage(
                  receipt.id,
                  selectedItems.length > 0 ? selectedItems.map((i) => i.id) : undefined,
                )
              }
            >
              Печать{selectedSuffix}
            </Button>
            <Button
              startIcon={<QrCode2Icon />}
              size="small"
              onClick={() =>
                labelsAction.open(
                  printedItems.map((i) => ({
                    ...i.catalogItem,
                    count: isDraftOrPlanned ? i.plannedCount : (i.receivedCount ?? 0),
                  })),
                )
              }
            >
              Этикетки{selectedSuffix}
            </Button>
            {selectedItems.length > 0 && (
              // the header checkbox reaches only rows the search leaves visible
              <Tooltip title="Снять выделение">
                <IconButton size="small" onClick={clear}>
                  <CloseIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
          </>
        )}
        {isDraftOrPlanned && canEdit && (
          <Button startIcon={<EditIcon />} size="small" onClick={() => setEditorOpen(true)}>
            Редактировать позиции
          </Button>
        )}
      </Stack>
      {isProcessing && (
        <Stack spacing={1} sx={{mb: 1.5}}>
          <TextField
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Поиск по каталогу..."
            size="small"
            fullWidth
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    {catalogSearchQuery.isFetching ? (
                      <CircularProgress size={16} />
                    ) : (
                      <SearchIcon fontSize="small" />
                    )}
                  </InputAdornment>
                ),
              },
            }}
          />
        </Stack>
      )}
      {isMobile && visibleItems.length > 0 && (
        <FormControlLabel
          sx={{mb: 1}}
          control={
            <Checkbox
              size="small"
              checked={allPageSelected}
              indeterminate={selectedItems.length > 0 && !allPageSelected}
              onChange={() => toggleAll()}
            />
          }
          label={
            <Typography variant="body2" color="text.secondary">
              {allPageSelected
                ? "Снять выделение"
                : selectedItems.length > 0
                  ? `Выбрано: ${selectedItems.length}`
                  : "Выбрать все"}
            </Typography>
          }
        />
      )}

      {items.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Нет позиций
        </Typography>
      ) : isDraftOrPlanned ? (
        isMobile ? (
          <Stack spacing={1}>
            {items.map((item) => (
              <Paper key={item.id} variant="outlined" sx={selectedCardSx(isSelected(item.id))}>
                <Stack spacing={0.5}>
                  <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                    <CardCheckbox checked={isSelected(item.id)} onCheck={() => toggle(item)} />
                    <CatalogItemCell item={item} onOpen={setCatalogItemId} />
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Запланировано: {item.plannedCount}
                  </Typography>
                  {item.notes && (
                    <Typography variant="body2" color="text.secondary">
                      {item.notes}
                    </Typography>
                  )}
                </Stack>
              </Paper>
            ))}
          </Stack>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <SelectionTableCell
                  checked={allPageSelected}
                  indeterminate={!allPageSelected && somePageSelected}
                  onCheck={() => toggleAll()}
                />
                <TableCell>Товар</TableCell>
                <TableCell align="right">Запланировано</TableCell>
                <TableCell>Примечание</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id} hover selected={isSelected(item.id)}>
                  <SelectionTableCell
                    checked={isSelected(item.id)}
                    onCheck={(extendRange) => toggle(item, extendRange)}
                  />
                  <TableCell>
                    <CatalogItemCell item={item} onOpen={setCatalogItemId} />
                  </TableCell>
                  <TableCell align="right">{item.plannedCount}</TableCell>
                  <NotesTableCell notes={item.notes} />
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )
      ) : isProcessing ? (
        <Stack spacing={2}>
          {isMobile ? (
            <Stack spacing={1}>
              {visibleItems.map((item) => (
                <ProcessingItemCard
                  key={item.id}
                  item={item}
                  receipt={receipt}
                  onUpdate={onUpdate}
                  onOpenCatalog={setCatalogItemId}
                  selected={isSelected(item.id)}
                  onToggleSelect={toggle}
                />
              ))}
              {isSearchActive && visibleItems.length === 0 && !catalogSearchQuery.isFetching && (
                <Typography variant="body2" color="text.secondary">
                  Нет совпадений в приёмке
                </Typography>
              )}
            </Stack>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <SelectionTableCell
                    checked={allPageSelected}
                    indeterminate={!allPageSelected && somePageSelected}
                    onCheck={() => toggleAll()}
                  />
                  <TableCell padding="checkbox" />
                  <TableCell>Товар</TableCell>
                  <TableCell align="right">Запланировано</TableCell>
                  <TableCell align="left">Принято</TableCell>
                  <TableCell align="right">Расхождение</TableCell>
                  <TableCell align="right">Размещено</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {visibleItems.map((item) => (
                  <ProcessingItemRow
                    key={item.id}
                    item={item}
                    receipt={receipt}
                    onUpdate={onUpdate}
                    onOpenCatalog={setCatalogItemId}
                    selected={isSelected(item.id)}
                    onToggleSelect={toggle}
                  />
                ))}
                {isSearchActive && visibleItems.length === 0 && !catalogSearchQuery.isFetching && (
                  <TableRow>
                    <TableCell colSpan={8} sx={{color: "text.secondary", textAlign: "center"}}>
                      Нет совпадений в приёмке
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}

          {isSearchActive && extraCatalogItems.length > 0 && (
            <Box>
              <Typography variant="subtitle2" color="text.secondary" sx={{mb: 0.5}}>
                Добавить в приёмку
              </Typography>
              {isMobile ? (
                <Stack spacing={1}>
                  {extraCatalogItems.map((cat) => (
                    <Paper key={cat.id} variant="outlined" sx={{p: 1.5}}>
                      <Stack spacing={1}>
                        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                          <CatalogItemTypeChip type={cat.type} />
                          <Typography variant="body2">{cat.fullName}</Typography>
                        </Stack>
                        <Button
                          size="small"
                          variant="outlined"
                          startIcon={<AddIcon />}
                          disabled={quickAddPendingIds.has(cat.id)}
                          onClick={() =>
                            quickAddMutation.mutate({
                              path: {id: receipt.id},
                              body: {catalogItemId: cat.id},
                            })
                          }
                          sx={{alignSelf: "flex-start"}}
                        >
                          Добавить
                        </Button>
                      </Stack>
                    </Paper>
                  ))}
                </Stack>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Товар</TableCell>
                      <TableCell />
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {extraCatalogItems.map((cat) => (
                      <TableRow key={cat.id}>
                        <TableCell>
                          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                            <CatalogItemTypeChip type={cat.type} />
                            <Typography variant="body2">{cat.fullName}</Typography>
                          </Stack>
                        </TableCell>
                        <TableCell align="right">
                          <Button
                            size="small"
                            startIcon={<AddIcon />}
                            disabled={quickAddPendingIds.has(cat.id)}
                            onClick={() =>
                              quickAddMutation.mutate({
                                path: {id: receipt.id},
                                body: {catalogItemId: cat.id},
                              })
                            }
                          >
                            Добавить
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Box>
          )}
        </Stack>
      ) : isReadOnly ? (
        isMobile ? (
          <Stack spacing={1}>
            {items.map((item) => {
              const totalPlaced = calcTotalPlaced(item);
              return (
                <Paper key={item.id} variant="outlined" sx={selectedCardSx(isSelected(item.id))}>
                  <Stack spacing={0.75}>
                    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                      <CardCheckbox checked={isSelected(item.id)} onCheck={() => toggle(item)} />
                      <CatalogItemCell item={item} onOpen={setCatalogItemId} />
                    </Stack>
                    <Stack direction="row" spacing={2}>
                      <Typography variant="body2" color="text.secondary">
                        Запланировано: {item.plannedCount}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Принято: {item.receivedCount ?? "—"}
                      </Typography>
                    </Stack>
                    <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
                      <Typography variant="body2" color="text.secondary">
                        Расхождение:
                      </Typography>
                      <DiscrepancyText planned={item.plannedCount} received={item.receivedCount} />
                    </Stack>
                    {totalPlaced > 0 && (
                      <Typography variant="body2" color="text.secondary">
                        Размещено: {totalPlaced}
                      </Typography>
                    )}
                    {item.placements.length > 0 && (
                      <>
                        <Divider />
                        <Stack spacing={0.5}>
                          {item.placements.map((p) => (
                            <PlacementDisplay key={p.id} placement={p} />
                          ))}
                        </Stack>
                      </>
                    )}
                  </Stack>
                </Paper>
              );
            })}
          </Stack>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <SelectionTableCell
                  checked={allPageSelected}
                  indeterminate={!allPageSelected && somePageSelected}
                  onCheck={() => toggleAll()}
                />
                <TableCell>Товар</TableCell>
                <TableCell align="right">Запланировано</TableCell>
                <TableCell align="right">Принято</TableCell>
                <TableCell align="right">Расхождение</TableCell>
                <TableCell align="right">Размещено</TableCell>
                <TableCell>Размещения</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => {
                const totalPlaced = calcTotalPlaced(item);
                return (
                  <TableRow key={item.id} hover selected={isSelected(item.id)}>
                    <SelectionTableCell
                      checked={isSelected(item.id)}
                      onCheck={(extendRange) => toggle(item, extendRange)}
                    />
                    <TableCell>
                      <CatalogItemCell item={item} onOpen={setCatalogItemId} />
                    </TableCell>
                    <TableCell align="right">{item.plannedCount}</TableCell>
                    <TableCell align="right">{item.receivedCount ?? "—"}</TableCell>
                    <DiscrepancyCell planned={item.plannedCount} received={item.receivedCount} />
                    <TableCell align="right">{totalPlaced || "—"}</TableCell>
                    <TableCell>
                      {item.placements.length > 0 ? (
                        <Stack spacing={0.5}>
                          {item.placements.map((p) => (
                            <PlacementDisplay key={p.id} placement={p} />
                          ))}
                        </Stack>
                      ) : (
                        <Typography variant="body2" color="text.secondary">
                          —
                        </Typography>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )
      ) : null}

      {isDraftOrPlanned && (
        <ReceiptItemsEditorDrawer
          open={editorOpen}
          onClose={() => setEditorOpen(false)}
          receipt={receipt}
          onUpdate={(updated) => {
            onUpdate(updated);
            setEditorOpen(false);
            clear();
          }}
        />
      )}

      {isMobile && canPlaceSelected && (
        <Box
          sx={{
            position: "fixed",
            bottom: 24,
            right: 24,
            zIndex: 1200,
          }}
        >
          <Fab
            variant="extended"
            color="primary"
            onClick={() => setBatchDialogOpen(true)}
            sx={{gap: 1, whiteSpace: "nowrap"}}
          >
            <AddIcon />
            Разместить ({selectedStandardItems.length})
          </Fab>
        </Box>
      )}

      <BatchStandardPlacementDialog
        open={batchDialogOpen}
        onClose={() => setBatchDialogOpen(false)}
        receiptId={receipt.id}
        warehouseId={receipt.warehouseId}
        items={selectedStandardItems}
        onUpdate={(updated) => {
          onUpdate(updated);
          setBatchDialogOpen(false);
          clear();
        }}
      />

      {labelsAction.dialogs}

      <CatalogItemDrawer
        itemId={catalogItemId}
        onClose={() => setCatalogItemId(null)}
        onOpenItem={setCatalogItemId}
        backClosable
      />
    </Box>
  );
}

export default ReceiptItemsSection;
