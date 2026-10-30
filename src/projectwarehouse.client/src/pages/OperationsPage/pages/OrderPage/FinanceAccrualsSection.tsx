import {
  Alert,
  Box,
  Link,
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
import type {OrderFinanceAccrualDto} from "@/api/types.gen";
import {Link as RouterLink} from "react-router";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import {ACCRUAL_CATEGORY_LABELS} from "@/features/marketplace/accrualCategories";
import {formatDateOnly} from "@/utils/dateOnly";

const moneyFormat = new Intl.NumberFormat("ru-RU", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

function money(amount: number, currency: string | null | undefined) {
  return `${moneyFormat.format(amount)} ${currency || "(валюта не указана)"}`;
}

function AccrualDescription({row}: {row: OrderFinanceAccrualDto}) {
  return (
    <Box>
      <Typography variant="body2">
        {row.category === "sale" && row.amount < 0
          ? "Возврат продажи"
          : ACCRUAL_CATEGORY_LABELS[row.category]}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{overflowWrap: "anywhere"}}>
        {row.source === "buyoutReport" ? "Отчёт о выкупе" : row.rawTypeId || "Журнал начислений"}
        {` · № ${row.externalId}`}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
        {row.itemName || (row.sku ? `SKU ${row.sku}` : "Без привязки к позиции")}
        {row.exemplarId && ` · Экземпляр ${row.exemplarId}`}
      </Typography>
      {row.orderId && row.orderNumber != null ? (
        <Link component={RouterLink} to={`/operations/orders/${row.orderId}`} variant="caption">
          {formatOrderNumber(row.orderNumber)} · {row.postingNumber}
        </Link>
      ) : (
        <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
          Без привязки к заказу{row.unitNumber && ` · ${row.unitNumber}`}
        </Typography>
      )}
    </Box>
  );
}

export default function FinanceAccrualsSection({
  rows,
  title,
}: {
  rows: OrderFinanceAccrualDto[];
  title: string;
}) {
  const isMobile = useMediaQuery(useTheme().breakpoints.down("sm"));
  const currencies = [...new Set(rows.map((row) => row.currencyCode || ""))];

  return (
    <Stack spacing={2}>
      <Typography variant="subtitle1" sx={{fontWeight: 600}}>
        {title}
      </Typography>
      {rows.length === 0 ? (
        <Alert severity="info">Начисления ещё не получены.</Alert>
      ) : (
        <>
          {currencies.map((currency) => {
            const items = rows.filter((row) => (row.currencyCode || "") === currency);
            const sum = (pick: (row: OrderFinanceAccrualDto) => boolean) =>
              items.filter(pick).reduce((total, row) => total + Math.round(row.amount * 100), 0) /
              100;
            const metrics: [string, number][] = [
              ["Продажи по отчёту", sum((r) => r.category === "sale" && r.amount > 0)],
              ["Возвраты продаж", sum((r) => r.category === "sale" && r.amount < 0)],
              ["Удержания", sum((r) => r.category !== "sale" && r.amount < 0)],
              [
                "Прочие начисления и возвраты удержаний",
                sum((r) => r.category !== "sale" && r.amount > 0),
              ],
              ["Итог начислений", sum(() => true)],
            ];
            return (
              <Box
                key={currency}
                sx={{
                  display: "grid",
                  gridTemplateColumns: {xs: "1fr", sm: "repeat(3, 1fr)"},
                  gap: 1,
                }}
              >
                {metrics.map(([label, amount]) => (
                  <Paper key={label} variant="outlined" sx={{p: 1.5}}>
                    <Typography variant="caption" color="text.secondary">
                      {label}
                    </Typography>
                    <Typography
                      sx={{fontWeight: 600}}
                      color={amount < 0 ? "error.main" : "text.primary"}
                    >
                      {money(amount, currency)}
                    </Typography>
                  </Paper>
                ))}
              </Box>
            );
          })}
          <Typography variant="caption" color="text.secondary">
            Итог по загруженным операциям, без себестоимости и общих расходов магазина. Это
            начисления площадки, а не подтверждение перевода на банковский счёт.
          </Typography>
          {isMobile ? (
            <Stack spacing={1}>
              {rows.map((row) => (
                <Paper key={row.id} variant="outlined" sx={{p: 1.5}}>
                  <Stack direction="row" spacing={1} sx={{mb: 1, justifyContent: "space-between"}}>
                    <Typography variant="caption">{formatDateOnly(row.date)}</Typography>
                    <Typography
                      variant="body2"
                      sx={{fontWeight: 600}}
                      color={row.amount < 0 ? "error.main" : "success.main"}
                    >
                      {money(row.amount, row.currencyCode)}
                    </Typography>
                  </Stack>
                  <AccrualDescription row={row} />
                </Paper>
              ))}
            </Stack>
          ) : (
            <Table size="small" aria-label="Операции начислений">
              <TableHead>
                <TableRow>
                  <TableCell>Учётный день</TableCell>
                  <TableCell>Операция и товар</TableCell>
                  <TableCell align="right">Сумма</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell sx={{whiteSpace: "nowrap", verticalAlign: "top"}}>
                      {formatDateOnly(row.date)}
                    </TableCell>
                    <TableCell>
                      <AccrualDescription row={row} />
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{
                        whiteSpace: "nowrap",
                        verticalAlign: "top",
                        color: row.amount < 0 ? "error.main" : "success.main",
                      }}
                    >
                      {money(row.amount, row.currencyCode)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </>
      )}
    </Stack>
  );
}
