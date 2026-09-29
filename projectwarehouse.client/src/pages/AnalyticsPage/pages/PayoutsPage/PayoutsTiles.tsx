import type {ReactNode} from "react";
import {Box, ButtonBase, Paper, Stack, Typography} from "@mui/material";
import type {PayoutsBucket, PayoutsDto, PayoutsMoneyDto, PayoutsRowDto} from "@/api/types.gen";
import {formatMoney} from "@/components/analytics/analyticsFormat";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {shopWideNote} from "./payoutsCategories";
import type {PostingsTarget} from "./payoutsPostingsTarget";
import {formatEstimate} from "./payoutsFormat";

interface Tile {
  title: string;
  caption: string;
  amount: (m: PayoutsMoneyDto) => string;
  postings: (m: PayoutsMoneyDto) => number;
  note?: (m: PayoutsMoneyDto) => string | null;
  color?: string;
  /** Sums shop estimates, so a shop without a payout ratio leaves it incomplete. */
  estimate?: boolean;
  /** The bucket whose postings a click on a currency line lists. */
  bucket?: PayoutsBucket;
}

/** A shop without a payout ratio has postings in this tile's bucket, so its estimate is missing from the sum. */
function isPartial(tile: Tile, rows: PayoutsRowDto[], currencyCode: string): boolean {
  return rows.some((r) =>
    r.money.some(
      (m) => m.currencyCode === currencyCode && m.payoutRatio == null && tile.postings(m) > 0,
    ),
  );
}

function Tile({
  tile,
  totals,
  rows,
  onOpenPostings,
}: {
  tile: Tile;
  totals: PayoutsMoneyDto[];
  rows: PayoutsRowDto[];
  onOpenPostings: (target: PostingsTarget) => void;
}) {
  const lines: ReactNode =
    totals.length === 0 ? (
      <Typography variant="h5">—</Typography>
    ) : (
      totals.map((m) => {
        const {bucket} = tile;
        const clickable = bucket && tile.postings(m) > 0;
        return (
          <ButtonBase
            key={m.currencyCode}
            disabled={!clickable}
            onClick={() => bucket && onOpenPostings({bucket, currencyCode: m.currencyCode})}
            sx={{
              display: "block",
              textAlign: "left",
              width: "calc(100% + 8px)",
              borderRadius: 1,
              mx: -0.5,
              px: 0.5,
              "&:hover": {bgcolor: "action.hover"},
            }}
          >
            <Typography variant="h5" sx={{color: tile.color}}>
              {tile.amount(m)}
            </Typography>
            <Typography variant="caption" color="text.secondary" component="div">
              {[pluralCount(tile.postings(m), NOUNS.posting), tile.note?.(m)]
                .filter(Boolean)
                .join(" · ")}
            </Typography>
            {tile.estimate && isPartial(tile, rows, m.currencyCode) && (
              <Typography variant="caption" color="warning.main" component="div">
                без магазинов, у которых нет доли выплаты
              </Typography>
            )}
          </ButtonBase>
        );
      })
    );

  return (
    <Paper variant="outlined" sx={{p: 2}}>
      <Typography variant="body2" color="text.secondary">
        {tile.title}
      </Typography>
      <Stack spacing={0.5} sx={{mt: 1}}>
        {lines}
      </Stack>
      <Typography variant="caption" color="text.secondary" component="div" sx={{mt: 0.5}}>
        {tile.caption}
      </Typography>
    </Paper>
  );
}

function PayoutsTiles({
  data,
  onOpenPostings,
}: {
  data: PayoutsDto;
  onOpenPostings: (target: PostingsTarget) => void;
}) {
  const {payoutOverdueDays, payoutNotAccruedDays} = data.settings;
  const tiles: Tile[] = [
    {
      title: "В пути",
      caption: "оценка по доле выплаты",
      amount: (m) => formatEstimate(m.inTransit, m.currencyCode),
      postings: (m) => m.inTransitPostings,
      estimate: true,
      bucket: "inTransit",
    },
    {
      title: "Доставлено, не начислено",
      caption: `оценка · доставлено меньше ${payoutNotAccruedDays} дн. назад`,
      amount: (m) => formatEstimate(m.deliveredNotAccrued, m.currencyCode),
      postings: (m) => m.deliveredNotAccruedPostings,
      estimate: true,
      bucket: "deliveredNotAccrued",
    },
    {
      title: "Начислено",
      caption: "точно · по журналу начислений за период",
      amount: (m) => formatMoney(m.accrued, m.currencyCode),
      postings: (m) => m.accruedPostings,
      note: shopWideNote,
    },
    {
      title: `В пути дольше ${payoutOverdueDays} дн.`,
      caption: "скорее всего, застряло",
      amount: (m) => formatEstimate(m.inTransitOverdue, m.currencyCode),
      postings: (m) => m.inTransitOverduePostings,
      estimate: true,
      bucket: "inTransitOverdue",
      color: "warning.main",
    },
    {
      title: "Площадка не начислила",
      caption: `доставлено ${payoutNotAccruedDays}+ дн. назад, продажи в журнале нет · вне долга`,
      amount: (m) => formatEstimate(m.notAccruedByMarketplace, m.currencyCode),
      postings: (m) => m.notAccruedByMarketplacePostings,
      estimate: true,
      bucket: "notAccruedByMarketplace",
      color: "error.main",
    },
  ];

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {
          xs: "minmax(0, 1fr)",
          sm: "repeat(2, minmax(0, 1fr))",
          lg: "repeat(5, minmax(0, 1fr))",
        },
        gap: 2,
      }}
    >
      {tiles.map((tile) => (
        <Tile
          key={tile.title}
          tile={tile}
          totals={data.totals}
          rows={data.rows}
          onOpenPostings={onOpenPostings}
        />
      ))}
    </Box>
  );
}

export default PayoutsTiles;
