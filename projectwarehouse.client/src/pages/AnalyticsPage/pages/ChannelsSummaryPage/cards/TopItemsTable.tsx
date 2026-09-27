import {Table, TableBody, TableCell, TableHead, TableRow, Typography} from "@mui/material";
import type {TopItemDto} from "@/api/types.gen";
import CatalogItemLink from "@/components/catalog/CatalogItemLink";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import {formatMoney, formatNumber, formatPercent} from "../channelsSummaryUtils";

interface TopItemsTableProps {
  /** Ranked items with their place, which a filtered list must keep. */
  rows: {rank: number; item: TopItemDto}[];
  currencyCode: string | null | undefined;
}

function TopItemsTable({rows, currencyCode}: TopItemsTableProps) {
  const openCatalogItem = useOpenCatalogItem();

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell sx={{width: 32}}>#</TableCell>
          <TableCell>Товар</TableCell>
          <TableCell align="right">Штук</TableCell>
          {currencyCode && <TableCell align="right">Сумма</TableCell>}
          <TableCell align="right">Доля</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {rows.map(({rank, item}) => (
          <TableRow key={item.catalogItemId}>
            <TableCell sx={{color: "text.secondary"}}>{rank}</TableCell>
            <TableCell sx={{wordBreak: "break-word"}}>
              <CatalogItemLink catalogItemId={item.catalogItemId} onOpen={openCatalogItem}>
                <Typography variant="body2">{item.name}</Typography>
              </CatalogItemLink>
            </TableCell>
            <TableCell align="right">{formatNumber(item.units)}</TableCell>
            {currencyCode && (
              <TableCell align="right" sx={{whiteSpace: "nowrap"}}>
                {item.money == null ? "—" : formatMoney(item.money, currencyCode)}
              </TableCell>
            )}
            <TableCell align="right">{formatPercent(item.share)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export default TopItemsTable;
