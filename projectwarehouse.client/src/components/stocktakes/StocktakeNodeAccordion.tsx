import {useMemo, useState} from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import SaveIcon from "@mui/icons-material/Save";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  stocktakesGetNodeStockOptions,
  stocktakesGetNodeStockQueryKey,
  stocktakesSyncNodeItemsMutation,
} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import StocktakeAddItemModal from "@/components/stocktakes/StocktakeAddItemModal";
import {deltaColor, formatDelta} from "@/components/stocktakes/stocktakeUtils";
import type {DraftRow} from "@/components/stocktakes/stocktakeDraft";
import {
  buildDraftRows,
  draftToRequest,
  hasDifferences,
  rowKey,
} from "@/components/stocktakes/stocktakeDraft";
import type {StocktakeDto, StocktakeNodeDto} from "@/api/types.gen";

type RowEdit = Partial<Pick<DraftRow, "counted" | "notes">>;

interface StocktakeNodeAccordionProps {
  stocktake: StocktakeDto;
  node: StocktakeNodeDto;
  canEdit: boolean;
  onUpdated: (updated: StocktakeDto) => void;
  onOpenCatalogItem: (id: string) => void;
}

function StocktakeNodeAccordion({
  stocktake,
  node,
  canEdit,
  onUpdated,
  onOpenCatalogItem,
}: StocktakeNodeAccordionProps) {
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const [expanded, setExpanded] = useState(false);
  const [addOpen, setAddOpen] = useState(false);

  // Unsaved work is kept apart from server data, so a refetch refreshes the baseline
  // without wiping what the operator typed
  const [edits, setEdits] = useState<Record<string, RowEdit>>({});
  const [added, setAdded] = useState<DraftRow[]>([]);
  const [removed, setRemoved] = useState<string[]>([]);

  const stockQuery = useQuery({
    ...stocktakesGetNodeStockOptions({path: {id: stocktake.id, nodeId: node.storagePlaceNodeId}}),
    enabled: expanded,
  });

  const stock = stockQuery.data;

  const baseline = useMemo(() => (stock ? buildDraftRows(node, stock) : null), [stock, node]);

  const rows = useMemo(() => {
    if (!baseline) return null;
    return [...baseline, ...added]
      .filter((row) => !removed.includes(row.key))
      .map((row) => ({...row, ...edits[row.key]}));
  }, [baseline, added, removed, edits]);

  const dirty = Object.keys(edits).length > 0 || added.length > 0 || removed.length > 0;

  const resetDraft = () => {
    setEdits({});
    setAdded([]);
    setRemoved([]);
  };

  const mutation = useMutation({
    ...stocktakesSyncNodeItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdated(data);
      resetDraft();
      void queryClient.invalidateQueries({
        queryKey: stocktakesGetNodeStockQueryKey({
          path: {id: stocktake.id, nodeId: node.storagePlaceNodeId},
        }),
      });
      enqueueSnackbar("Ячейка сохранена", {variant: "success"});
    },
    onError: (err) =>
      enqueueSnackbar(extractErrorMessage(err) || "Не удалось сохранить подсчёт", {
        variant: "error",
      }),
  });

  const patchRow = (key: string, patch: RowEdit) => {
    setEdits((prev) => ({...prev, [key]: {...prev[key], ...patch}}));
  };

  const removeRow = (key: string) => {
    setAdded((prev) => prev.filter((r) => r.key !== key));
    setRemoved((prev) => (prev.includes(key) ? prev : [...prev, key]));
  };

  const handleAdd = (row: Omit<DraftRow, "key" | "expected" | "notes">) => {
    const key = rowKey(row.kind, row.catalogItemId, row.inventoryNumber);
    setAddOpen(false);
    if (rows?.some((r) => r.key === key)) {
      enqueueSnackbar("Такая позиция уже есть в списке", {variant: "info"});
      return;
    }
    setRemoved((prev) => prev.filter((k) => k !== key));

    // Re-adding a row that exists in the baseline must restore it, not push a second copy
    if (baseline?.some((r) => r.key === key)) {
      patchRow(key, {counted: row.counted});
      return;
    }
    setAdded((prev) => [...prev, {...row, key, expected: 0, notes: ""}]);
  };

  const differencesCount = rows?.filter((r) => r.counted !== r.expected).length ?? 0;

  return (
    <Accordion expanded={expanded} onChange={(_, v) => setExpanded(v)} disableGutters>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center", flexGrow: 1, pr: 2}}>
          <Typography sx={{flexGrow: 1}}>{formatStoragePlaceNodeName(node.nodePath)}</Typography>
          {rows && differencesCount > 0 && (
            <Chip label={`Расхождений: ${differencesCount}`} size="small" color="warning" />
          )}
          {node.items.length > 0 && <Chip label={`${node.items.length} поз.`} size="small" />}
          {dirty && <Chip label="Не сохранено" size="small" color="info" variant="outlined" />}
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        {stockQuery.isLoading || !rows ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
            <CircularProgress size={28} />
          </Box>
        ) : (
          <Stack spacing={2}>
            {rows.length === 0 ? (
              <Alert severity="info">
                В ячейке ничего не числится. Если товар всё же найден — добавьте его как излишек.
              </Alert>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Товар</TableCell>
                    <TableCell align="right">Ожидается</TableCell>
                    <TableCell align="right">Посчитано</TableCell>
                    <TableCell align="right">Δ</TableCell>
                    <TableCell>Примечание</TableCell>
                    <TableCell width={48} />
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((row) => {
                    const delta = row.counted - row.expected;
                    return (
                      <TableRow key={row.key} hover>
                        <TableCell>
                          <CatalogItemLink
                            catalogItemId={row.catalogItemId}
                            onOpen={onOpenCatalogItem}
                          >
                            <Stack>
                              <Typography variant="body2">{row.catalogItemName}</Typography>
                              {row.inventoryNumber && (
                                <Typography
                                  variant="caption"
                                  color="text.secondary"
                                  sx={{fontFamily: "monospace"}}
                                >
                                  {row.inventoryNumber}
                                </Typography>
                              )}
                            </Stack>
                          </CatalogItemLink>
                        </TableCell>
                        <TableCell align="right">{row.expected}</TableCell>
                        <TableCell align="right">
                          {row.kind === "unit" ? (
                            <Checkbox
                              size="small"
                              checked={row.counted > 0}
                              disabled={!canEdit || mutation.isPending}
                              onChange={(e) =>
                                patchRow(row.key, {counted: e.target.checked ? 1 : 0})
                              }
                            />
                          ) : (
                            <ClampedIntegerField
                              value={row.counted}
                              min={0}
                              size="small"
                              disabled={!canEdit || mutation.isPending}
                              onCommit={(v) => patchRow(row.key, {counted: v})}
                              sx={{width: 96}}
                            />
                          )}
                        </TableCell>
                        <TableCell align="right" sx={{color: deltaColor(delta)}}>
                          {formatDelta(delta)}
                        </TableCell>
                        <TableCell>
                          <TextField
                            size="small"
                            fullWidth
                            value={row.notes}
                            disabled={!canEdit || mutation.isPending}
                            onChange={(e) => patchRow(row.key, {notes: e.target.value})}
                          />
                        </TableCell>
                        <TableCell>
                          {canEdit && row.expected === 0 && (
                            <IconButton
                              size="small"
                              disabled={mutation.isPending}
                              onClick={() => removeRow(row.key)}
                            >
                              <DeleteIcon fontSize="small" />
                            </IconButton>
                          )}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}

            {hasDifferences(rows) && (
              <Alert severity="warning">
                Позиции с расхождением будут скорректированы при завершении инвентаризации.
              </Alert>
            )}

            {canEdit && (
              <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
                <Button
                  size="small"
                  startIcon={<AddIcon />}
                  onClick={() => setAddOpen(true)}
                  disabled={mutation.isPending}
                >
                  Добавить товар (излишек)
                </Button>
                <Button
                  size="small"
                  variant="contained"
                  startIcon={<SaveIcon />}
                  disabled={!dirty || mutation.isPending}
                  loading={mutation.isPending}
                  onClick={() =>
                    mutation.mutate({
                      path: {id: stocktake.id, nodeId: node.storagePlaceNodeId},
                      body: draftToRequest(rows),
                    })
                  }
                >
                  Сохранить
                </Button>
              </Stack>
            )}
          </Stack>
        )}
      </AccordionDetails>

      <StocktakeAddItemModal open={addOpen} onClose={() => setAddOpen(false)} onAdd={handleAdd} />
    </Accordion>
  );
}

export default StocktakeNodeAccordion;
