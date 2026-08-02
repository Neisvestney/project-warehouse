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
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import SearchIcon from "@mui/icons-material/Search";
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  catalogGetAllOptions,
  receiptsDeletePlacementMutation,
  receiptsQuickAddItemMutation,
  receiptsUpdateReceivedCountMutation,
} from "@/api/@tanstack/react-query.gen";
import {useDebounce} from "@/hooks/useDebounce";
import {useHasPermission} from "@/hooks/usePermission";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import ReceiptItemsEditorDrawer from "@/components/receipts/ReceiptItemsEditorDrawer";
import AddPlacementDialog from "@/components/receipts/AddPlacementDialog";
import BatchStandardPlacementDialog from "@/components/receipts/BatchStandardPlacementDialog";
import type {ReceiptDto, ReceiptItemDto, ReceiptItemPlacementDto} from "@/api/types.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

const VIRTUAL_TYPES = new Set(["productGroup", "variation", "bundle"]);

function CatalogItemCell({item, onOpen}: {item: ReceiptItemDto; onOpen: (id: string) => void}) {
  return (
    <Stack
      direction="row"
      spacing={1}
      sx={{
        alignItems: "center",
        cursor: "pointer",
        width: "fit-content",
        "& .open-icon": {visibility: "hidden"},
        "&:hover .open-icon": {visibility: "visible"},
      }}
      onClick={() => onOpen(item.catalogItemId)}
    >
      <CatalogItemTypeChip type={item.catalogItem.type} />
      <Typography variant="body2">{item.catalogItem.fullName}</Typography>
      <OpenInNewIcon className="open-icon" sx={{fontSize: 14, color: "text.secondary"}} />
    </Stack>
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

function calcTotalPlaced(item: ReceiptItemDto): number {
  return item.placements.reduce((sum, p) => sum + (p.count || (p.unitInventoryItemId ? 1 : 0)), 0);
}

interface ReceiptItemsSectionProps {
  receipt: ReceiptDto;
  onUpdate: (updated: ReceiptDto) => void;
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
  const [value, setValue] = useState<string>(
    item.receivedCount !== null && item.receivedCount !== undefined
      ? String(item.receivedCount)
      : "",
  );

  const mutation = useMutation({
    ...receiptsUpdateReceivedCountMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => onUpdateItem(data),
  });

  const save = () => {
    const parsed = value === "" ? null : Number(value);
    if (value !== "" && (isNaN(parsed!) || parsed! < 0)) return;
    const current =
      item.receivedCount !== null && item.receivedCount !== undefined
        ? String(item.receivedCount)
        : "";
    if (value === current) return;
    mutation.mutate({
      path: {id: receiptId, itemId: item.id},
      body: {receivedCount: parsed},
    });
  };

  return (
    <TextField
      value={value}
      onChange={(e) => setValue(e.target.value)}
      onBlur={save}
      onKeyDown={(e) => {
        if (e.key === "Enter") save();
      }}
      type="number"
      size="small"
      sx={{width: 80}}
      disabled={mutation.isPending}
      placeholder="—"
      slotProps={{htmlInput: {min: 0}}}
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
  onToggleSelect: (id: string) => void;
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
  const isStandard = item.catalogItem.type === "standard";

  return (
    <>
      <TableRow hover selected={selected}>
        <TableCell padding="checkbox">
          {isStandard && canProcess ? (
            <Checkbox size="small" checked={selected} onChange={() => onToggleSelect(item.id)} />
          ) : null}
        </TableCell>
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
      {placementDialogOpen && (
        <AddPlacementDialog
          open
          onClose={() => setPlacementDialogOpen(false)}
          receiptId={receipt.id}
          item={item}
          warehouseId={receipt.warehouseId}
          onUpdate={(updatedItem) => {
            onUpdate(mergeItem(updatedItem));
            setPlacementDialogOpen(false);
          }}
        />
      )}
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
  onToggleSelect: (id: string) => void;
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
    meta: {suppressGlobalError: true},
    onSuccess: (data) => onUpdate(mergeItem(data)),
  });

  const totalPlaced = useMemo(() => calcTotalPlaced(item), [item]);
  const isVirtual = VIRTUAL_TYPES.has(item.catalogItem.type);
  const isStandard = item.catalogItem.type === "standard";

  return (
    <>
      <Paper
        variant="outlined"
        sx={{
          p: 1.5,
          outline: selected ? "2px solid" : undefined,
          outlineColor: selected ? "primary.main" : undefined,
        }}
      >
        <Stack spacing={1}>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            {isStandard && canProcess && (
              <Checkbox
                size="small"
                checked={selected}
                onChange={() => onToggleSelect(item.id)}
                sx={{p: 0}}
              />
            )}
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
      {placementDialogOpen && (
        <AddPlacementDialog
          open
          onClose={() => setPlacementDialogOpen(false)}
          receiptId={receipt.id}
          item={item}
          warehouseId={receipt.warehouseId}
          onUpdate={(updatedItem) => {
            onUpdate(mergeItem(updatedItem));
            setPlacementDialogOpen(false);
          }}
        />
      )}
    </>
  );
}

const PLACEABLE_TYPES: Array<"standard" | "unit"> = ["standard", "unit"];

function ReceiptItemsSection({receipt, onUpdate}: ReceiptItemsSectionProps) {
  const [editorOpen, setEditorOpen] = useState(false);
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);
  const [selectedItemIds, setSelectedItemIds] = useState<Set<string>>(new Set());
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

  const visibleStandardItems = useMemo(
    () => visibleItems.filter((i) => i.catalogItem.type === "standard"),
    [visibleItems],
  );

  const toggleSelect = useCallback((id: string) => {
    setSelectedItemIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const allStandardSelected =
    visibleStandardItems.length > 0 && visibleStandardItems.every((i) => selectedItemIds.has(i.id));
  const someStandardSelected = visibleStandardItems.some((i) => selectedItemIds.has(i.id));

  const toggleSelectAll = useCallback(() => {
    setSelectedItemIds((prev) => {
      const next = new Set(prev);
      if (allStandardSelected) {
        visibleStandardItems.forEach((i) => next.delete(i.id));
      } else {
        visibleStandardItems.forEach((i) => next.add(i.id));
      }
      return next;
    });
  }, [allStandardSelected, visibleStandardItems]);

  const selectedItems = useMemo(
    () => items.filter((i) => selectedItemIds.has(i.id)),
    [items, selectedItemIds],
  );

  return (
    <Box>
      <Stack direction="row" sx={{alignItems: "center", mb: 1, gap: 1}}>
        <Typography variant="h6" sx={{flexGrow: 1}}>
          Позиции
        </Typography>
        {isProcessing && !isMobile && canProcess && selectedItemIds.size > 0 && (
          <Button
            variant="contained"
            size="small"
            startIcon={<AddIcon />}
            onClick={() => setBatchDialogOpen(true)}
          >
            Разместить ({selectedItemIds.size})
          </Button>
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
          {isMobile && canProcess && visibleStandardItems.length > 0 && (
            <FormControlLabel
              control={
                <Checkbox
                  size="small"
                  checked={allStandardSelected}
                  indeterminate={someStandardSelected && !allStandardSelected}
                  onChange={toggleSelectAll}
                />
              }
              label={
                <Typography variant="body2" color="text.secondary">
                  {allStandardSelected
                    ? "Снять выделение"
                    : someStandardSelected
                      ? `Выбрано: ${selectedItemIds.size}`
                      : "Выбрать все"}
                </Typography>
              }
            />
          )}
        </Stack>
      )}

      {items.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Нет позиций
        </Typography>
      ) : isDraftOrPlanned ? (
        isMobile ? (
          <Stack spacing={1}>
            {items.map((item) => (
              <Paper key={item.id} variant="outlined" sx={{p: 1.5}}>
                <Stack spacing={0.5}>
                  <CatalogItemCell item={item} onOpen={setCatalogItemId} />
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
                <TableCell>Товар</TableCell>
                <TableCell align="right">Запланировано</TableCell>
                <TableCell>Примечание</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    <CatalogItemCell item={item} onOpen={setCatalogItemId} />
                  </TableCell>
                  <TableCell align="right">{item.plannedCount}</TableCell>
                  <TableCell sx={{color: "text.secondary"}}>{item.notes ?? "—"}</TableCell>
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
                  selected={selectedItemIds.has(item.id)}
                  onToggleSelect={toggleSelect}
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
                  <TableCell padding="checkbox">
                    {canProcess && visibleStandardItems.length > 0 && (
                      <Checkbox
                        size="small"
                        checked={allStandardSelected}
                        indeterminate={someStandardSelected && !allStandardSelected}
                        onChange={toggleSelectAll}
                      />
                    )}
                  </TableCell>
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
                    selected={selectedItemIds.has(item.id)}
                    onToggleSelect={toggleSelect}
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
                <Paper key={item.id} variant="outlined" sx={{p: 1.5}}>
                  <Stack spacing={0.75}>
                    <CatalogItemCell item={item} onOpen={setCatalogItemId} />
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
                  <TableRow key={item.id}>
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

      {isDraftOrPlanned && editorOpen && (
        <ReceiptItemsEditorDrawer
          open
          onClose={() => setEditorOpen(false)}
          receipt={receipt}
          onUpdate={(updated) => {
            onUpdate(updated);
            setEditorOpen(false);
          }}
        />
      )}

      {isMobile && isProcessing && canProcess && selectedItemIds.size > 0 && (
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
            Разместить ({selectedItemIds.size})
          </Fab>
        </Box>
      )}

      {batchDialogOpen && (
        <BatchStandardPlacementDialog
          open
          onClose={() => setBatchDialogOpen(false)}
          receiptId={receipt.id}
          warehouseId={receipt.warehouseId}
          items={selectedItems}
          onUpdate={(updated) => {
            onUpdate(updated);
            setBatchDialogOpen(false);
            setSelectedItemIds(new Set());
          }}
        />
      )}

      <CatalogItemDrawer
        itemId={catalogItemId}
        onClose={() => setCatalogItemId(null)}
        onOpenItem={setCatalogItemId}
      />
    </Box>
  );
}

export default ReceiptItemsSection;
