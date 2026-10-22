import {useState} from "react";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from "@mui/material";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import {useQuery} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {ordersGetCompositionSummary} from "@/api";
import type {OrderCompositionSummaryItemDto, SortOrder} from "@/api/types.gen";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CatalogItemLink} from "@/components/catalog/CatalogItemLink";
import QueryError from "@/components/QueryError";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useDrawerLocalState} from "@/hooks/useDrawerLocalState";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {copyToClipboard} from "@/utils/clipboardUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

type SortBy = "name" | "quantity" | "orderCount";

const COLUMNS: {key: SortBy; label: string; align: "left" | "right"}[] = [
  {key: "name", label: "Позиция", align: "left"},
  {key: "quantity", label: "Кол-во, шт.", align: "right"},
  {key: "orderCount", label: "Заказов", align: "right"},
];

function compareItems(
  a: OrderCompositionSummaryItemDto,
  b: OrderCompositionSummaryItemDto,
  sortBy: SortBy,
) {
  switch (sortBy) {
    case "name":
      return a.catalogItemName.localeCompare(b.catalogItemName, "ru");
    case "quantity":
      return a.quantity - b.quantity;
    case "orderCount":
      return a.orderCount - b.orderCount;
  }
}

function formatForClipboard(items: OrderCompositionSummaryItemDto[], totalQuantity: number) {
  const lines = items.map(
    (i) => `${i.catalogItemName}${i.article ? ` (${i.article})` : ""} — ${i.quantity} шт.`,
  );
  lines.push(`Итого: ${pluralCount(items.length, NOUNS.position)}, ${totalQuantity} шт.`);
  return lines.join("\n");
}

interface OrderCompositionSummaryDialogProps {
  /** Orders to sum; `null` keeps the dialog closed. */
  orderIds: string[] | null;
  onClose: () => void;
}

/** Box components of the selected orders summed per catalog item. */
export default function OrderCompositionSummaryDialog({
  orderIds,
  onClose,
}: OrderCompositionSummaryDialogProps) {
  const open = orderIds != null;
  const [shownIds, releaseShown] = useRetainedValue(orderIds);

  useBackClosable(open, onClose);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{
        transition: {onExited: releaseShown},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownIds && <OrderCompositionSummaryContent orderIds={shownIds} onClose={onClose} />}
    </Dialog>
  );
}

function OrderCompositionSummaryContent({
  orderIds,
  onClose,
}: {
  orderIds: string[];
  onClose: () => void;
}) {
  const {enqueueSnackbar} = useSnackbar();
  const [sortBy, setSortBy] = useState<SortBy>("quantity");
  const [sortOrder, setSortOrder] = useState<SortOrder>("desc");
  const [openedCatalogItemId, openCatalogDrawer, closeCatalogDrawer] = useDrawerLocalState();

  const query = useQuery({
    queryKey: ["ordersCompositionSummary", orderIds],
    queryFn: async ({signal}) => {
      const response = await ordersGetCompositionSummary({
        body: {orderIds},
        signal,
        throwOnError: true,
      });
      return response.data;
    },
    meta: {suppressGlobalError: true},
  });

  const items = [...(query.data?.items ?? [])].sort((a, b) => {
    const primary = compareItems(a, b, sortBy);
    const result = primary !== 0 ? primary : compareItems(a, b, "name");
    return sortOrder === "asc" ? result : -result;
  });
  const totalQuantity = items.reduce((sum, i) => sum + i.quantity, 0);

  const handleSortClick = (column: SortBy) => {
    if (sortBy === column) {
      setSortOrder(sortOrder === "asc" ? "desc" : "asc");
      return;
    }
    setSortBy(column);
    setSortOrder(column === "name" ? "asc" : "desc");
  };

  const handleCopy = async () => {
    const copied = await copyToClipboard(formatForClipboard(items, totalQuantity));
    enqueueSnackbar(copied ? "Скопировано" : "Не удалось скопировать", {
      variant: copied ? "success" : "error",
    });
  };

  return (
    <>
      <DialogTitle>
        Состав {pluralCount(orderIds.length, {one: "заказа", few: "заказов", many: "заказов"})}
      </DialogTitle>
      <DialogContent dividers sx={{p: 0}}>
        {query.isPending ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
            <CircularProgress />
          </Box>
        ) : query.isError ? (
          <QueryError error={query.error} />
        ) : items.length === 0 ? (
          <Typography sx={{color: "text.secondary", textAlign: "center", py: 4}}>
            В выбранных заказах нет позиций
          </Typography>
        ) : (
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                {COLUMNS.map((c) => (
                  <TableCell key={c.key} align={c.align}>
                    <TableSortLabel
                      active={sortBy === c.key}
                      direction={sortBy === c.key ? sortOrder : "asc"}
                      onClick={() => handleSortClick(c.key)}
                    >
                      {c.label}
                    </TableSortLabel>
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.catalogItemId}>
                  <TableCell>
                    <CatalogItemLink catalogItemId={item.catalogItemId} onOpen={openCatalogDrawer}>
                      <Typography variant="body2">{item.catalogItemName}</Typography>
                    </CatalogItemLink>
                    {item.article && (
                      <Typography variant="caption" sx={{color: "text.secondary"}}>
                        {item.article}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right">{item.quantity.toLocaleString("ru-RU")}</TableCell>
                  <TableCell align="right">{item.orderCount.toLocaleString("ru-RU")}</TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell>Всего: {pluralCount(items.length, NOUNS.position)}</TableCell>
                <TableCell align="right">{totalQuantity.toLocaleString("ru-RU")}</TableCell>
                <TableCell />
              </TableRow>
            </TableFooter>
          </Table>
        )}
      </DialogContent>
      <DialogActions>
        <Button
          startIcon={<ContentCopyIcon />}
          onClick={handleCopy}
          disabled={items.length === 0}
          sx={{mr: "auto"}}
        >
          Копировать
        </Button>
        <Button onClick={onClose}>Закрыть</Button>
      </DialogActions>
      <CatalogItemDrawer
        itemId={openedCatalogItemId}
        onClose={closeCatalogDrawer}
        onOpenItem={openCatalogDrawer}
        backClosable
      />
    </>
  );
}
