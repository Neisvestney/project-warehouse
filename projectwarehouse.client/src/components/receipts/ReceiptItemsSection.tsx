import {useState, useMemo} from "react";
import {
  Box,
  Button,
  Chip,
  Collapse,
  Divider,
  IconButton,
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
import {useMutation} from "@tanstack/react-query";
import {
  receiptsDeletePlacementMutation,
  receiptsUpdateReceivedCountMutation,
} from "@/api/@tanstack/react-query.gen";
import {useHasPermission} from "@/hooks/usePermission";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import ReceiptItemsEditorDrawer from "@/components/receipts/ReceiptItemsEditorDrawer";
import AddPlacementDialog from "@/components/receipts/AddPlacementDialog";
import type {ReceiptDto, ReceiptItemDto, ReceiptItemPlacementDto} from "@/api/types.gen";

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
  return item.placements.reduce(
    (sum, p) =>
      sum + (p.count || (p.unitInventoryItemId || p.assembledBundleInventoryItemId ? 1 : 0)),
    0,
  );
}

interface ReceiptItemsSectionProps {
  receipt: ReceiptDto;
  onUpdate: (updated: ReceiptDto) => void;
}

function PlacementDisplay({placement}: {placement: ReceiptItemPlacementDto}) {
  const path = placement.nodePath.join(" / ");
  if (placement.inventoryNumber) {
    return (
      <Typography variant="body2">
        {path} — инв. {placement.inventoryNumber}
      </Typography>
    );
  }
  if (placement.assembledBundleInventoryItemId) {
    return <Typography variant="body2">{path} — комплект</Typography>;
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
}: {
  item: ReceiptItemDto;
  receipt: ReceiptDto;
  onUpdate: (data: ReceiptDto) => void;
  onOpenCatalog: (id: string) => void;
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
      <TableRow hover>
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
        <TableCell colSpan={7} sx={{pb: 0, pt: 0}}>
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
}: {
  item: ReceiptItemDto;
  receipt: ReceiptDto;
  onUpdate: (data: ReceiptDto) => void;
  onOpenCatalog: (id: string) => void;
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

  return (
    <>
      <Paper variant="outlined" sx={{p: 1.5}}>
        <Stack spacing={1}>
          <CatalogItemCell item={item} onOpen={onOpenCatalog} />
          <Typography variant="body2" color="text.secondary">
            Запланировано: {item.plannedCount}
          </Typography>
          <Stack direction="row" spacing={2} sx={{alignItems: "center"}}>
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
          </Stack>
          {totalPlaced > 0 && (
            <Typography variant="body2" color="text.secondary">
              Размещено:{" "}
              <Chip label={totalPlaced} size="small" color="primary" variant="outlined" />
            </Typography>
          )}
          <Stack direction="row" spacing={1}>
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

function ReceiptItemsSection({receipt, onUpdate}: ReceiptItemsSectionProps) {
  const [editorOpen, setEditorOpen] = useState(false);
  const [catalogItemId, setCatalogItemId] = useState<string | null>(null);
  const canEdit = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);
  const {status, items} = receipt;

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("lg"));

  const isDraftOrPlanned = status === "draft" || status === "planned";
  const isProcessing = status === "processing";
  const isReadOnly = status === "finished" || status === "canceled";

  return (
    <Box>
      <Stack direction="row" sx={{alignItems: "center", mb: 1}}>
        <Typography variant="h6" sx={{flexGrow: 1}}>
          Позиции
        </Typography>
        {isDraftOrPlanned && canEdit && (
          <Button startIcon={<EditIcon />} size="small" onClick={() => setEditorOpen(true)}>
            Редактировать позиции
          </Button>
        )}
      </Stack>

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
        isMobile ? (
          <Stack spacing={1}>
            {items.map((item) => (
              <ProcessingItemCard
                key={item.id}
                item={item}
                receipt={receipt}
                onUpdate={onUpdate}
                onOpenCatalog={setCatalogItemId}
              />
            ))}
          </Stack>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
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
              {items.map((item) => (
                <ProcessingItemRow
                  key={item.id}
                  item={item}
                  receipt={receipt}
                  onUpdate={onUpdate}
                  onOpenCatalog={setCatalogItemId}
                />
              ))}
            </TableBody>
          </Table>
        )
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

      <CatalogItemDrawer
        itemId={catalogItemId}
        onClose={() => setCatalogItemId(null)}
        onOpenItem={setCatalogItemId}
      />
    </Box>
  );
}

export default ReceiptItemsSection;
