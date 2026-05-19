import {useEffect} from "react";
import {
  Box,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {inboundOrdersGetItemsComparisonOptions} from "@/api/@tanstack/react-query.gen";
import type {ComparisonItemDto, ItemDifferenceDto} from "@/api/types.gen";
import TableRowEmpty from "@/components/TableRowEmpty";
import QueryError from "@/components/QueryError";

function getItemName(item: ComparisonItemDto | ItemDifferenceDto): string {
  const ci = item.catalogItemWithCharacteristic;
  return `${ci.catalogItem.name} / ${ci.characteristic}`;
}

interface SectionTableProps {
  title: string;
  colSpan?: number;
  children: React.ReactNode;
  badge?: React.ReactNode;
}

function SectionTable({title, badge, children}: SectionTableProps) {
  return (
    <Box>
      <Stack direction="row" spacing={1} sx={{alignItems: "center", mb: 1}}>
        <Typography variant="body2" sx={{fontWeight: "medium"}} color="text.secondary">
          {title}
        </Typography>
        {badge}
      </Stack>
      {children}
    </Box>
  );
}

interface Props {
  orderId: string;
  onHasProcessedItemsChange?: (has: boolean) => void;
}

function ItemsComparisonSection({orderId, onHasProcessedItemsChange}: Props) {
  const {data, isLoading, isError, error} = useQuery({
    ...inboundOrdersGetItemsComparisonOptions({path: {id: orderId}}),
    meta: {suppressGlobalError: true},
  });

  useEffect(() => {
    onHasProcessedItemsChange?.((data?.processedItems?.length ?? 0) > 0);
  }, [data, onHasProcessedItemsChange]);

  if (isLoading) {
    return (
      <Paper sx={{p: 3, display: "flex", justifyContent: "center"}}>
        <CircularProgress />
      </Paper>
    );
  }

  if (isError) return <QueryError error={error} />;
  if (!data) return null;

  return (
    <Paper>
      <Stack spacing={3} sx={{p: 3}}>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", justifyContent: "space-between"}}
        >
          <Typography variant="subtitle1" sx={{fontWeight: "medium"}}>
            Товары
          </Typography>
          <Stack direction="row" spacing={1}>
            {data.totalShortageCount > 0 && (
              <Chip
                label={`Нехватка: ${data.totalShortageCount}`}
                color="error"
                size="small"
                variant="outlined"
              />
            )}
            {data.totalSurplusCount > 0 && (
              <Chip
                label={`Излишки: ${data.totalSurplusCount}`}
                color="success"
                size="small"
                variant="outlined"
              />
            )}
          </Stack>
        </Stack>

        <SectionTable title="Заявленные товары">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Товар</TableCell>
                <TableCell>Артикул</TableCell>
                <TableCell sx={{width: 80}}>Кол-во</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.declaredItems.length === 0 ? (
                <TableRowEmpty colSpan={3} message="Нет заявленных товаров" />
              ) : (
                data.declaredItems.map((item) => (
                  <TableRow key={item.catalogItemWithCharacteristic.id}>
                    <TableCell>{getItemName(item)}</TableCell>
                    <TableCell>{item.catalogItemWithCharacteristic.catalogItem.article}</TableCell>
                    <TableCell>{item.count}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </SectionTable>

        <SectionTable title="Обработанные товары">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Товар</TableCell>
                <TableCell>Артикул</TableCell>
                <TableCell sx={{width: 80}}>Кол-во</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.processedItems.length === 0 ? (
                <TableRowEmpty colSpan={3} message="Нет обработанных товаров" />
              ) : (
                data.processedItems.map((item) => (
                  <TableRow key={item.catalogItemWithCharacteristic.id}>
                    <TableCell>{getItemName(item)}</TableCell>
                    <TableCell>{item.catalogItemWithCharacteristic.catalogItem.article}</TableCell>
                    <TableCell>{item.count}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </SectionTable>

        {data.shortages.length > 0 && (
          <SectionTable
            title="Нехватка"
            badge={<Chip label={data.totalShortageCount} color="error" size="small" />}
          >
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Товар</TableCell>
                  <TableCell>Заявлено</TableCell>
                  <TableCell>Обработано</TableCell>
                  <TableCell>Разница</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.shortages.map((item) => (
                  <TableRow
                    key={item.catalogItemWithCharacteristic.id}
                    sx={{"& td": {color: "error.main"}}}
                  >
                    <TableCell>{getItemName(item)}</TableCell>
                    <TableCell>{item.declaredCount}</TableCell>
                    <TableCell>{item.processedCount}</TableCell>
                    <TableCell>−{item.differenceCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </SectionTable>
        )}

        {data.surpluses.length > 0 && (
          <SectionTable
            title="Излишки"
            badge={<Chip label={data.totalSurplusCount} color="success" size="small" />}
          >
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Товар</TableCell>
                  <TableCell>Заявлено</TableCell>
                  <TableCell>Обработано</TableCell>
                  <TableCell>Разница</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.surpluses.map((item) => (
                  <TableRow
                    key={item.catalogItemWithCharacteristic.id}
                    sx={{"& td": {color: "success.main"}}}
                  >
                    <TableCell>{getItemName(item)}</TableCell>
                    <TableCell>{item.declaredCount}</TableCell>
                    <TableCell>{item.processedCount}</TableCell>
                    <TableCell>+{item.differenceCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </SectionTable>
        )}
      </Stack>
    </Paper>
  );
}

export default ItemsComparisonSection;
