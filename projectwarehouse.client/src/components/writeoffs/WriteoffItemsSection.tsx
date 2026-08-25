import {useCallback, useState} from "react";
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
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import NotesTableCell from "@/components/NotesTableCell";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import WriteoffItemsEditorDrawer from "@/components/writeoffs/WriteoffItemsEditorDrawer";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface WriteoffItemsSectionProps {
  writeoff: WriteoffDto;
  /** Lifted so the page can hold the edit lock while the items editor is open. */
  onEditingChange?: (isEditing: boolean) => void;
}

function itemDisplayCount(item: WriteoffDto["items"][number]): string {
  if (item.inventoryNumber) return `[${item.inventoryNumber}]`;
  return String(item.count);
}

function itemType(item: WriteoffDto["items"][number]) {
  if (item.unitInventoryItemId) return "unit" as const;
  return "standard" as const;
}

function ItemNameCell({
  item,
  onOpen,
}: {
  item: WriteoffDto["items"][number];
  onOpen: (id: string) => void;
}) {
  const label = (
    <>
      <CatalogItemTypeChip type={itemType(item)} />
      <Typography variant="body2">{item.catalogItemName}</Typography>
    </>
  );

  if (!item.catalogItemId) {
    return (
      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
        {label}
      </Stack>
    );
  }

  return (
    <CatalogItemLink catalogItemId={item.catalogItemId} onOpen={onOpen}>
      {label}
    </CatalogItemLink>
  );
}

function WriteoffItemsSection({writeoff, onEditingChange}: WriteoffItemsSectionProps) {
  const [drawerOpen, setDrawerOpenState] = useState(false);

  const setDrawerOpen = useCallback(
    (value: boolean) => {
      setDrawerOpenState(value);
      onEditingChange?.(value);
    },
    [onEditingChange],
  );
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] =
    useDrawerSearchParamsState("catalogItem");
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
                          <ItemNameCell item={item} onOpen={openCatalogDrawer} />
                        </TableCell>
                        <TableCell>{itemDisplayCount(item)}</TableCell>
                        <NotesTableCell notes={item.notes} />
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

      <CatalogItemDrawer
        itemId={openedCatalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
      />
    </>
  );
}

export default WriteoffItemsSection;
