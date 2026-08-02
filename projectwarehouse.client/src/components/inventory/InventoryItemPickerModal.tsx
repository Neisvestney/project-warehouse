import {useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import {useQuery} from "@tanstack/react-query";
import {
  inventoryItemsGetAllOptions,
  inventoryItemsGetAllUnitsOptions,
} from "@/api/@tanstack/react-query.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import type {CatalogItemType} from "@/api/types.gen";
import type {InventoryItemSummaryDto, UnitInventoryItemDto} from "@/api/types.gen";

export type SelectedInventoryItem =
  | {
      type: "standard";
      catalogItemId: string;
      catalogItemName: string;
      count: number;
      available: number;
    }
  | {type: "unit"; unitItemId: string; inventoryNumber: string; catalogItemName: string};

interface InventoryItemPickerModalProps {
  open: boolean;
  onClose: () => void;
  nodeId: string;
  onConfirm: (items: SelectedInventoryItem[]) => void;
}

const PAGE_SIZE = 50;

function itemToCatalogType(item: SelectedInventoryItem): CatalogItemType {
  if (item.type === "unit") return "unit";
  return "standard";
}

// ── Standard accordion ────────────────────────────────────────────────────────

function StandardAccordion({
  nodeId,
  open,
  onAdd,
}: {
  nodeId: string;
  open: boolean;
  onAdd: (items: SelectedInventoryItem[]) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [page, setPage] = useState(1);
  const [checked, setChecked] = useState<Set<string>>(new Set());
  const [counts, setCounts] = useState<Record<string, number>>({});

  const query = useQuery({
    ...inventoryItemsGetAllOptions({
      query: {nodeId, page, pageSize: PAGE_SIZE, catalogItemType: "standard"},
    }),
    enabled: open && expanded,
    meta: {suppressGlobalError: true},
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;

  const toggleChecked = (id: string) => {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleAdd = () => {
    const toAdd: SelectedInventoryItem[] = items
      .filter((item: InventoryItemSummaryDto) => checked.has(item.catalogItemId))
      .map((item: InventoryItemSummaryDto) => ({
        type: "standard" as const,
        catalogItemId: item.catalogItemId,
        catalogItemName: item.catalogItem.fullName,
        count: counts[item.catalogItemId] ?? 1,
        available: item.count,
      }));
    onAdd(toAdd);
    setChecked(new Set());
    setCounts({});
  };

  return (
    <Accordion expanded={expanded} onChange={(_, v) => setExpanded(v)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="body2" sx={{fontWeight: 500}}>
          Стандартные товары
        </Typography>
      </AccordionSummary>
      <AccordionDetails sx={{p: 0}}>
        {query.isLoading ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
            <CircularProgress size={28} />
          </Box>
        ) : items.length === 0 ? (
          <Typography color="text.secondary" sx={{px: 2, py: 2}} variant="body2">
            Нет стандартных товаров в этой ячейке
          </Typography>
        ) : (
          <>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell padding="checkbox" />
                  <TableCell>Товар</TableCell>
                  <TableCell align="right">Доступно</TableCell>
                  <TableCell align="right">Кол-во</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((item: InventoryItemSummaryDto) => {
                  const isChecked = checked.has(item.catalogItemId);
                  return (
                    <TableRow key={item.catalogItemId} hover>
                      <TableCell padding="checkbox">
                        <Checkbox
                          size="small"
                          checked={isChecked}
                          onChange={() => toggleChecked(item.catalogItemId)}
                        />
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{item.catalogItem.fullName}</Typography>
                        <Typography variant="caption" color="text.secondary">
                          {item.catalogItem.article}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        <Typography variant="body2">{item.count}</Typography>
                      </TableCell>
                      <TableCell align="right" sx={{width: 90}}>
                        <TextField
                          type="number"
                          size="small"
                          value={counts[item.catalogItemId] ?? 1}
                          onChange={(e) => {
                            const val = Math.max(1, Math.min(item.count, Number(e.target.value)));
                            setCounts((prev) => ({...prev, [item.catalogItemId]: val}));
                          }}
                          disabled={!isChecked}
                          slotProps={{
                            htmlInput: {min: 1, max: item.count, style: {textAlign: "right"}},
                          }}
                          sx={{width: 80}}
                        />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>

            <TablePagination
              component="div"
              count={total}
              page={page - 1}
              rowsPerPage={PAGE_SIZE}
              rowsPerPageOptions={[]}
              onPageChange={(_, newPage) => setPage(newPage + 1)}
              labelDisplayedRows={({from, to, count}) => `${from}–${to} из ${count}`}
            />
          </>
        )}

        <Box sx={{px: 2, py: 1.5}}>
          <Button
            size="small"
            variant="contained"
            disabled={checked.size === 0}
            onClick={handleAdd}
          >
            Выбрать выделенное ({checked.size})
          </Button>
        </Box>
      </AccordionDetails>
    </Accordion>
  );
}

// ── Unit accordion ────────────────────────────────────────────────────────────

function UnitAccordion({
  nodeId,
  open,
  onAdd,
}: {
  nodeId: string;
  open: boolean;
  onAdd: (items: SelectedInventoryItem[]) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [page, setPage] = useState(1);
  const [checked, setChecked] = useState<Set<string>>(new Set());

  const query = useQuery({
    ...inventoryItemsGetAllUnitsOptions({
      query: {nodeId, page, pageSize: PAGE_SIZE},
    }),
    enabled: open && expanded,
    meta: {suppressGlobalError: true},
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;

  const toggleChecked = (id: string) => {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleAdd = () => {
    const toAdd: SelectedInventoryItem[] = items
      .filter((item: UnitInventoryItemDto) => checked.has(item.id))
      .map((item: UnitInventoryItemDto) => ({
        type: "unit" as const,
        unitItemId: item.id,
        inventoryNumber: item.inventoryNumber,
        catalogItemName: item.catalogItem.fullName,
      }));
    onAdd(toAdd);
    setChecked(new Set());
  };

  return (
    <Accordion expanded={expanded} onChange={(_, v) => setExpanded(v)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="body2" sx={{fontWeight: 500}}>
          Единичные товары
        </Typography>
      </AccordionSummary>
      <AccordionDetails sx={{p: 0}}>
        {query.isLoading ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
            <CircularProgress size={28} />
          </Box>
        ) : items.length === 0 ? (
          <Typography color="text.secondary" sx={{px: 2, py: 2}} variant="body2">
            Нет единичных товаров в этой ячейке
          </Typography>
        ) : (
          <>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell padding="checkbox" />
                  <TableCell>Инв. номер</TableCell>
                  <TableCell>Товар</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((item: UnitInventoryItemDto) => (
                  <TableRow key={item.id} hover>
                    <TableCell padding="checkbox">
                      <Checkbox
                        size="small"
                        checked={checked.has(item.id)}
                        onChange={() => toggleChecked(item.id)}
                      />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                        {item.inventoryNumber}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{item.catalogItem.fullName}</Typography>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <TablePagination
              component="div"
              count={total}
              page={page - 1}
              rowsPerPage={PAGE_SIZE}
              rowsPerPageOptions={[]}
              onPageChange={(_, newPage) => setPage(newPage + 1)}
              labelDisplayedRows={({from, to, count}) => `${from}–${to} из ${count}`}
            />
          </>
        )}

        <Box sx={{px: 2, py: 1.5}}>
          <Button
            size="small"
            variant="contained"
            disabled={checked.size === 0}
            onClick={handleAdd}
          >
            Выбрать выделенное ({checked.size})
          </Button>
        </Box>
      </AccordionDetails>
    </Accordion>
  );
}

// ── Main modal ────────────────────────────────────────────────────────────────

function InventoryItemPickerModal({
  open,
  onClose,
  nodeId,
  onConfirm,
}: InventoryItemPickerModalProps) {
  const [selected, setSelected] = useState<SelectedInventoryItem[]>([]);

  const addItems = (incoming: SelectedInventoryItem[]) => {
    setSelected((prev) => {
      const next = [...prev];
      for (const item of incoming) {
        const isDuplicate =
          item.type === "standard"
            ? next.some((s) => s.type === "standard" && s.catalogItemId === item.catalogItemId)
            : next.some((s) => s.type === "unit" && s.unitItemId === item.unitItemId);
        if (!isDuplicate) next.push(item);
      }
      return next;
    });
  };

  const removeItem = (index: number) => {
    setSelected((prev) => prev.filter((_, i) => i !== index));
  };

  const updateCount = (index: number, count: number) => {
    setSelected((prev) =>
      prev.map((item, i) => (i === index && item.type === "standard" ? {...item, count} : item)),
    );
  };

  const handleClose = () => {
    setSelected([]);
    onClose();
  };

  const handleConfirm = () => {
    onConfirm(selected);
    setSelected([]);
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="sm"
      fullWidth
      sx={{"& .MuiDialog-paper": {height: "calc(100vh - 64px)"}}}
    >
      <DialogTitle sx={{pb: 0}}>
        <Stack direction="row" sx={{alignItems: "center"}}>
          <Typography variant="h6" sx={{flexGrow: 1}}>
            Выбор товаров
          </Typography>
          <IconButton onClick={handleClose} size="small">
            <CloseIcon />
          </IconButton>
        </Stack>
      </DialogTitle>

      <DialogContent sx={{p: 0, display: "flex", flexDirection: "column", overflow: "hidden"}}>
        {/* Selected items — fixed top section */}
        <Box sx={{px: 2, pt: 2, pb: 1, flexShrink: 0}}>
          <Typography variant="subtitle2" gutterBottom>
            Выбранные товары
          </Typography>
          {selected.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{fontStyle: "italic"}}>
              Нет выбранных товаров
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Тип</TableCell>
                  <TableCell>Товар</TableCell>
                  <TableCell align="right">Кол-во</TableCell>
                  <TableCell padding="checkbox" />
                </TableRow>
              </TableHead>
              <TableBody>
                {selected.map((item, i) => (
                  <TableRow key={item.type === "standard" ? item.catalogItemId : item.unitItemId}>
                    <TableCell>
                      <CatalogItemTypeChip type={itemToCatalogType(item)} size="small" />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{item.catalogItemName}</Typography>
                      {item.type === "unit" && (
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{fontFamily: "monospace"}}
                        >
                          {item.inventoryNumber}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {item.type === "standard" ? (
                        <TextField
                          type="number"
                          size="small"
                          value={item.count}
                          onChange={(e) => {
                            const val = Math.max(
                              1,
                              Math.min(item.available, Number(e.target.value)),
                            );
                            updateCount(i, val);
                          }}
                          slotProps={{
                            htmlInput: {min: 1, max: item.available, style: {textAlign: "right"}},
                          }}
                          sx={{width: 80}}
                        />
                      ) : (
                        <Typography variant="body2">1 шт.</Typography>
                      )}
                    </TableCell>
                    <TableCell padding="checkbox">
                      <IconButton size="small" onClick={() => removeItem(i)}>
                        <CloseIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>

        <Divider sx={{my: 1, flexShrink: 0}} />

        {/* Accordions — scrollable */}
        <Box sx={{overflow: "auto", flex: 1}}>
          <StandardAccordion nodeId={nodeId} open={open} onAdd={addItems} />
          <UnitAccordion nodeId={nodeId} open={open} onAdd={addItems} />
        </Box>
      </DialogContent>

      <DialogActions>
        <Button onClick={handleClose}>Отмена</Button>
        <Button variant="contained" disabled={selected.length === 0} onClick={handleConfirm}>
          Подтвердить
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default InventoryItemPickerModal;
