import {useState} from "react";
import {
  Box,
  Button,
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
import EditIcon from "@mui/icons-material/Edit";
import type {WriteoffDto} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import WriteoffItemsEditorDrawer from "@/components/writeoffs/WriteoffItemsEditorDrawer";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface WriteoffItemsSectionProps {
  writeoff: WriteoffDto;
}

function itemDisplayCount(item: WriteoffDto["items"][number]): string {
  if (item.inventoryNumber) return `[${item.inventoryNumber}]`;
  return String(item.count);
}

function itemType(item: WriteoffDto["items"][number]) {
  if (item.unitInventoryItemId) return "unit" as const;
  return "standard" as const;
}

function WriteoffItemsSection({writeoff}: WriteoffItemsSectionProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const isDraft = writeoff.status === "draft";

  // Group items by source node
  const nodeGroups = writeoff.items.reduce<
    Map<string, {path: string[]; items: typeof writeoff.items}>
  >((acc, item) => {
    if (!acc.has(item.sourceNodeId)) {
      acc.set(item.sourceNodeId, {path: item.sourceNodePath, items: []});
    }
    acc.get(item.sourceNodeId)!.items.push(item);
    return acc;
  }, new Map());

  return (
    <>
      <Paper>
        <Stack
          direction="row"
          sx={{alignItems: "center", px: 2, pt: 2, pb: writeoff.items.length > 0 ? 1 : 2}}
        >
          <Typography variant="h6" sx={{flexGrow: 1}}>
            Товары
            {writeoff.items.length > 0 && (
              <Chip
                label={writeoff.items.length}
                size="small"
                sx={{ml: 1, verticalAlign: "middle"}}
              />
            )}
          </Typography>
          {isDraft && (
            <Button size="small" startIcon={<EditIcon />} onClick={() => setDrawerOpen(true)}>
              Редактировать
            </Button>
          )}
        </Stack>

        {writeoff.items.length === 0 ? (
          <Box sx={{px: 2, pb: 2}}>
            <Typography variant="body2" color="text.secondary">
              Нет товаров
            </Typography>
          </Box>
        ) : (
          <Box>
            {[...nodeGroups.entries()].map(([nodeId, {path, items}]) => (
              <Box key={nodeId} sx={{mb: 2}}>
                <Typography variant="caption" color="text.secondary" sx={{px: 2}}>
                  {formatStoragePlaceNodeName(path)}
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Товар</TableCell>
                      <TableCell>Количество</TableCell>
                      <TableCell>Примечания</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {items.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell>
                          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                            <CatalogItemTypeChip type={itemType(item)} />
                            <Typography variant="body2">{item.catalogItemName}</Typography>
                          </Stack>
                        </TableCell>
                        <TableCell>{itemDisplayCount(item)}</TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary">
                            {item.notes || "—"}
                          </Typography>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            ))}
          </Box>
        )}
      </Paper>

      {isDraft && (
        <WriteoffItemsEditorDrawer
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          writeoff={writeoff}
        />
      )}
    </>
  );
}

export default WriteoffItemsSection;
