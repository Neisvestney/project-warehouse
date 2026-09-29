import {Box, Divider, Paper, Stack, Tooltip, Typography} from "@mui/material";
import type {
  PayoutsAppliedSettingsDto,
  PayoutsDto,
  PayoutsMoneyDto,
  PayoutsRowDto,
} from "@/api/types.gen";
import {formatMoney, formatPercent} from "@/components/analytics/analyticsFormat";
import ChannelChip from "@/components/analytics/ChannelChip";
import {formatDateOnly} from "@/utils/dateOnly";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import PayoutsAgeBar from "./PayoutsAgeBar";
import {isWithheld, shopWideNote, summarizeCategories} from "./payoutsCategories";
import {formatEstimate} from "./payoutsFormat";

function Figure({
  label,
  value,
  postings,
  note,
  color,
}: {
  label: string;
  value: string;
  postings: number;
  note?: string | null;
  color?: string;
}) {
  const caption = [postings > 0 ? pluralCount(postings, NOUNS.posting) : null, note]
    .filter(Boolean)
    .join(" · ");
  return (
    <Box sx={{minWidth: 0}}>
      <Typography variant="caption" color="text.secondary" component="div" noWrap>
        {label}
      </Typography>
      <Typography variant="subtitle1" sx={{color, whiteSpace: "nowrap"}}>
        {value}
      </Typography>
      <Typography variant="caption" color="text.secondary" component="div">
        {caption || " "}
      </Typography>
    </Box>
  );
}

function Withheld({money}: {money: PayoutsMoneyDto}) {
  const summary = summarizeCategories(money.categories);
  const lines = [
    ...summary.byPosting.map((l) => ({...l, key: `posting-${l.key}`})),
    ...summary.byShop.map((l) => ({
      ...l,
      key: `shop-${l.key}`,
      label: `${l.label} (по магазину)`,
    })),
  ].filter((l) => isWithheld(l.category));
  if (lines.length === 0) return null;

  return (
    <Tooltip
      title={
        <Stack>
          {lines.map((l) => (
            <span key={l.key}>
              {l.label}: {formatMoney(l.amount, money.currencyCode)} ({formatPercent(l.share)})
            </span>
          ))}
        </Stack>
      }
    >
      <Typography
        variant="body2"
        color="text.secondary"
        sx={{cursor: "help", alignSelf: "flex-start"}}
      >
        Удержано площадкой {formatMoney(summary.withheld, money.currencyCode)}
        {summary.withheldShare != null && ` · ${formatPercent(summary.withheldShare)} от продаж`}
      </Typography>
    </Tooltip>
  );
}

function MoneyBlock({
  money,
  settings,
}: {
  money: PayoutsMoneyDto;
  settings: PayoutsAppliedSettingsDto;
}) {
  const {payoutNotAccruedDays} = settings;
  return (
    <Stack spacing={1.5}>
      <Box sx={{display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 1.5}}>
        <Figure
          label="В пути"
          value={formatEstimate(money.inTransit, money.currencyCode)}
          postings={money.inTransitPostings}
        />
        <Figure
          label="Доставлено, не начислено"
          value={formatEstimate(money.deliveredNotAccrued, money.currencyCode)}
          postings={money.deliveredNotAccruedPostings}
        />
        <Figure
          label={`Не начислила за ${payoutNotAccruedDays}+ дн.`}
          value={formatEstimate(money.notAccruedByMarketplace, money.currencyCode)}
          postings={money.notAccruedByMarketplacePostings}
          color={money.notAccruedByMarketplacePostings > 0 ? "error.main" : undefined}
        />
        <Figure
          label="Начислено за период"
          value={formatMoney(money.accrued, money.currencyCode)}
          postings={money.accruedPostings}
          note={shopWideNote(money)}
        />
      </Box>
      <PayoutsAgeBar
        byAge={money.inTransitByAge}
        currencyCode={money.currencyCode}
        settings={settings}
      />
      <Withheld money={money} />
    </Stack>
  );
}

function ShopCard({row, settings}: {row: PayoutsRowDto; settings: PayoutsAppliedSettingsDto}) {
  const ratios = row.money.filter((m) => m.payoutRatio != null);

  return (
    <Paper variant="outlined" sx={{p: 2, display: "flex", flexDirection: "column", gap: 1.5}}>
      <Box>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", justifyContent: "space-between"}}
        >
          <Box sx={{minWidth: 0}}>
            <ChannelChip
              row={{
                kind: "marketplace",
                marketplaceAccountId: row.marketplaceAccountId,
                marketplaceType: row.marketplaceType,
                name: row.name,
              }}
            />
          </Box>
          <Tooltip
            title={`Доля выплаты за ${settings.payoutRatioWindowDays} дн. — по ней оцениваются суммы долга`}
          >
            <Typography
              variant="body2"
              color="text.secondary"
              sx={{cursor: "help", whiteSpace: "nowrap"}}
            >
              доля{" "}
              {ratios.length === 0
                ? "—"
                : ratios.map((m) => formatPercent(m.payoutRatio)).join(" / ")}
            </Typography>
          </Tooltip>
        </Stack>
        <Typography variant="caption" color="text.secondary" component="div" sx={{mt: 0.5}}>
          {row.coveredFrom
            ? `журнал с ${formatDateOnly(row.coveredFrom)}`
            : "журнала начислений нет"}
          {row.uncoveredPostings > 0 && (
            <Tooltip title="Доставлены до начала журнала, и продажи по ним в нём нет: площадка начислила их раньше. В суммы не входят">
              <Box component="span" sx={{cursor: "help"}}>
                {` · ${pluralCount(row.uncoveredPostings, NOUNS.posting)} до него`}
              </Box>
            </Tooltip>
          )}
          {row.buyoutsPending && (
            <Tooltip title="Отчёт о выкупах маркетплейсом ещё догружается фоновой синхронизацией. Выкупленные площадкой отправления до этой даты пока числятся неначисленными">
              <Box component="span" sx={{cursor: "help", color: "warning.main"}}>
                {row.buyoutsLoadedFrom
                  ? ` · выкупы с ${formatDateOnly(row.buyoutsLoadedFrom)}`
                  : " · выкупы ещё не загружены"}
              </Box>
            </Tooltip>
          )}
        </Typography>
      </Box>

      {row.money.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Нет отправлений в пути и начислений за период
        </Typography>
      ) : (
        <Stack spacing={2} divider={<Divider flexItem />}>
          {row.money.map((m) => (
            <MoneyBlock key={m.currencyCode} money={m} settings={settings} />
          ))}
        </Stack>
      )}
    </Paper>
  );
}

function PayoutsShops({data}: {data: PayoutsDto}) {
  if (data.rows.length === 0)
    return (
      <Paper variant="outlined" sx={{p: 3, textAlign: "center"}}>
        <Typography color="text.secondary">Нет выбранных магазинов</Typography>
      </Paper>
    );

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {
          xs: "minmax(0, 1fr)",
          md: "repeat(2, minmax(0, 1fr))",
          xl: "repeat(3, minmax(0, 1fr))",
        },
        gap: 2,
      }}
    >
      {data.rows.map((row) => (
        <ShopCard key={row.marketplaceAccountId} row={row} settings={data.settings} />
      ))}
    </Box>
  );
}

export default PayoutsShops;
