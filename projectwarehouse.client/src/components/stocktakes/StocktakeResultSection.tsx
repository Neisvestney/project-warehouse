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
} from "@mui/material";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import NotesTableCell from "@/components/NotesTableCell";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {deltaColor, formatDelta} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeDto} from "@/api/types.gen";

interface StocktakeResultSectionProps {
  stocktake: StocktakeDto;
}

function StocktakeResultSection({stocktake}: StocktakeResultSectionProps) {
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");

  const allItems = stocktake.nodes.flatMap((n) => n.items);
  // Read from appliedDelta, never from live stock — the books have moved on since
  const surplus = allItems.reduce((sum, i) => sum + Math.max(i.appliedDelta ?? 0, 0), 0);
  const shortage = allItems.reduce((sum, i) => sum - Math.min(i.appliedDelta ?? 0, 0), 0);
  const isCanceled = stocktake.status === "canceled";

  return (
    <Paper>
      <Stack spacing={2} sx={{p: 3}}>
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
                  {node.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <CatalogItemLink
                          catalogItemId={item.catalogItemId}
                          onOpen={openCatalogDrawer}
                        >
                          <Stack>
                            <Typography variant="body2">{item.catalogItemName}</Typography>
                            {item.inventoryNumber && (
                              <Typography
                                variant="caption"
                                color="text.secondary"
                                sx={{fontFamily: "monospace"}}
                              >
                                {item.inventoryNumber}
                              </Typography>
                            )}
                          </Stack>
                        </CatalogItemLink>
                      </TableCell>
                      <TableCell align="right">{item.countedQuantity}</TableCell>
                      <TableCell align="right" sx={{color: deltaColor(item.appliedDelta ?? 0)}}>
                        {item.appliedDelta == null ? "—" : formatDelta(item.appliedDelta)}
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
