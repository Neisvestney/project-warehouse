import {Box, Divider, Paper, Stack, Tooltip, Typography} from "@mui/material";
import type {PayoutsDto} from "@/api/types.gen";
import {formatMoney, formatPercent} from "@/components/analytics/analyticsFormat";
import {
  CATEGORY_LABELS,
  type CategoryLine,
  categoryColor,
  summarizeCategories,
} from "./payoutsCategories";

const UNKNOWN_HINT = "Типы начислений, которые провайдер не умеет разложить по статьям";

function Line({
  label,
  amount,
  share,
  currencyCode,
  color,
  strong,
}: {
  label: string;
  amount: number;
  share: number | null;
  currencyCode: string;
  color?: string;
  strong?: boolean;
}) {
  const weight = strong ? 500 : undefined;
  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "minmax(0, 1fr) auto 4.5rem",
        columnGap: 2,
        alignItems: "baseline",
        color,
      }}
    >
      <Typography variant="body2" sx={{fontWeight: weight}} noWrap>
        {label}
      </Typography>
      <Typography variant="body2" sx={{fontWeight: weight, whiteSpace: "nowrap"}}>
        {formatMoney(amount, currencyCode)}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{textAlign: "right"}}>
        {formatPercent(share)}
      </Typography>
    </Box>
  );
}

function Group({
  title,
  lines,
  sales,
  currencyCode,
}: {
  title: string;
  lines: CategoryLine[];
  sales: number;
  currencyCode: string;
}) {
  const total = lines.reduce((acc, l) => acc + l.amount, 0);
  const share = sales > 0 ? total / sales : null;

  return (
    <Stack spacing={0.5} sx={{minWidth: 0}}>
      <Typography variant="caption" color="text.secondary">
        {title}
      </Typography>
      {lines.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          —
        </Typography>
      ) : (
        lines.map((line) => {
          const row = (
            <Line
              label={CATEGORY_LABELS[line.category]}
              amount={line.amount}
              share={line.share}
              currencyCode={currencyCode}
              color={categoryColor(line.category)}
            />
          );
          return line.category === "unknown" ? (
            <Tooltip key={line.category} title={UNKNOWN_HINT}>
              <Box sx={{cursor: "help"}}>{row}</Box>
            </Tooltip>
          ) : (
            <Box key={line.category}>{row}</Box>
          );
        })
      )}
      {lines.length > 1 && (
        <Line label="Итого" amount={total} share={share} currencyCode={currencyCode} strong />
      )}
    </Stack>
  );
}

function PayoutsCategoriesCard({data}: {data: PayoutsDto}) {
  const currencies = data.totals.filter((m) => m.categories.length > 0);

  return (
    <Paper variant="outlined" sx={{p: 2}}>
      <Typography variant="body2" color="text.secondary">
        Начисления по статьям
      </Typography>
      <Typography variant="caption" color="text.secondary" component="div">
        точно · по журналу начислений за период, % — от продаж
      </Typography>

      {currencies.length === 0 ? (
        <Typography variant="h5" sx={{mt: 1}}>
          —
        </Typography>
      ) : (
        <Stack spacing={2} divider={<Divider flexItem />} sx={{mt: 1.5}}>
          {currencies.map((m) => {
            const summary = summarizeCategories(m.categories);
            return (
              <Stack key={m.currencyCode} spacing={1.5}>
                {currencies.length > 1 && (
                  <Typography variant="subtitle2">{m.currencyCode}</Typography>
                )}
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: {xs: "minmax(0, 1fr)", md: "repeat(2, minmax(0, 1fr))"},
                    gap: {xs: 2, md: 4},
                  }}
                >
                  <Group
                    title="По отправлениям"
                    lines={summary.byPosting}
                    sales={summary.sales}
                    currencyCode={m.currencyCode}
                  />
                  <Group
                    title="По магазину — без привязки к отправлениям"
                    lines={summary.byShop}
                    sales={summary.sales}
                    currencyCode={m.currencyCode}
                  />
                </Box>
                <Divider />
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: {xs: "minmax(0, 1fr)", md: "repeat(2, minmax(0, 1fr))"},
                    gap: {xs: 0.5, md: 4},
                  }}
                >
                  <Line
                    label="Удержано площадкой"
                    amount={summary.withheld}
                    share={summary.withheldShare}
                    currencyCode={m.currencyCode}
                    strong
                  />
                  <Line
                    label="Итого начислено"
                    amount={summary.total}
                    share={summary.sales > 0 ? summary.total / summary.sales : null}
                    currencyCode={m.currencyCode}
                    strong
                  />
                </Box>
              </Stack>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
}

export default PayoutsCategoriesCard;
