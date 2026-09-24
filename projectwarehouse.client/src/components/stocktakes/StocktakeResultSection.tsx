import {
  Alert,
  Box,
  Chip,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import NotesTableCell from "@/components/NotesTableCell";
import StocktakeItemName from "@/components/stocktakes/StocktakeItemName";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {deltaColor, formatDelta} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeDto, StocktakeItemDto} from "@/api/types.gen";

// Lines arrive in catalog order; a stable partition keeps that order inside each half
function differencesFirst(items: StocktakeItemDto[]): StocktakeItemDto[] {
  const changed = items.filter((i) => (i.appliedDelta ?? 0) !== 0);
  const unchanged = items.filter((i) => (i.appliedDelta ?? 0) === 0);
  return [...changed, ...unchanged];
}

function formatAppliedDelta(item: StocktakeItemDto): string {
  return item.appliedDelta == null ? "—" : formatDelta(item.appliedDelta);
}

interface StocktakeResultSectionProps {
  stocktake: StocktakeDto;
}

function StocktakeResultSection({stocktake}: StocktakeResultSectionProps) {
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const allItems = stocktake.nodes.flatMap((n) => n.items);
  // Read from appliedDelta, never from live stock — the books have moved on since
  const surplus = allItems.reduce((sum, i) => sum + Math.max(i.appliedDelta ?? 0, 0), 0);
  const shortage = allItems.reduce((sum, i) => sum - Math.min(i.appliedDelta ?? 0, 0), 0);
  const isCanceled = stocktake.status === "canceled";

  return (
    <Paper>
      <Stack spacing={2} sx={{p: {xs: 2, sm: 3}}}>
        <Stack direction="row" spacing={1.5} sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
          <Typography variant="h6">Результаты</Typography>
          {!isCanceled && (
            <>
              <Chip
                label={`Излишки: ${surplus}`}
                size="small"
                color={surplus > 0 ? "success" : "default"}
              />
              <Chip
                label={`Недостачи: ${shortage}`}
                size="small"
                color={shortage > 0 ? "error" : "default"}
              />
            </>
          )}
        </Stack>

        {isCanceled && (
          <Alert severity="warning">Инвентаризация отменена, остатки не менялись.</Alert>
        )}

        {stocktake.nodes.map((node) => (
          <Box key={node.id}>
            <Typography variant="subtitle2" sx={{mb: 0.5}}>
              {formatStoragePlaceNodeName(node.nodePath)}
            </Typography>
            {node.items.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                Ячейка проверена, позиций не зафиксировано.
              </Typography>
            ) : isMobile ? (
              <Stack spacing={1}>
                {differencesFirst(node.items).map((item) => (
                  <Paper key={item.id} variant="outlined" sx={{p: 1.5}}>
                    <Stack spacing={1}>
                      <StocktakeItemName
                        catalogItemId={item.catalogItemId}
                        catalogItemName={item.catalogItemName}
                        inventoryNumber={item.inventoryNumber}
                        onOpen={openCatalogDrawer}
                      />
                      <Stack direction="row" spacing={3}>
                        <Box>
                          <Typography variant="caption" color="text.secondary" component="div">
                            Посчитано
                          </Typography>
                          <Typography variant="body2">{item.countedQuantity}</Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" color="text.secondary" component="div">
                            Корректировка
                          </Typography>
                          <Typography
                            variant="body2"
                            sx={{color: deltaColor(item.appliedDelta ?? 0)}}
                          >
                            {formatAppliedDelta(item)}
                          </Typography>
                        </Box>
                      </Stack>
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
                    <TableCell align="right">Посчитано</TableCell>
                    <TableCell align="right">Корректировка</TableCell>
                    <TableCell>Примечание</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {differencesFirst(node.items).map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <StocktakeItemName
                          catalogItemId={item.catalogItemId}
                          catalogItemName={item.catalogItemName}
                          inventoryNumber={item.inventoryNumber}
                          onOpen={openCatalogDrawer}
                        />
                      </TableCell>
                      <TableCell align="right">{item.countedQuantity}</TableCell>
                      <TableCell align="right" sx={{color: deltaColor(item.appliedDelta ?? 0)}}>
                        {formatAppliedDelta(item)}
                      </TableCell>
                      <NotesTableCell notes={item.notes} />
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Box>
        ))}
      </Stack>

      <CatalogItemDrawer
        itemId={openedCatalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
      />
    </Paper>
  );
}

export default StocktakeResultSection;
