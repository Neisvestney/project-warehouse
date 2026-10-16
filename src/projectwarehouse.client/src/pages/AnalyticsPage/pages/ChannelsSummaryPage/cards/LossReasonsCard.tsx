import {
  Alert,
  Button,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsLossReasonsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsAbcSubject, AnalyticsLossKind, AnalyticsLossReasonsBy} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {extractErrorMessage} from "@/utils/errorUtils";
import {parseStep} from "@/components/analytics/channelsQuery";
import {formatNumber} from "@/components/analytics/analyticsFormat";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import type {PeriodSelection} from "@/components/analytics/period/periodSelection";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import {SUBJECT_LABELS, SUBJECT_TOOLTIPS, SUBJECTS} from "@/components/analytics/subjects/subjects";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import CardChannelSelect from "./CardChannelSelect";
import LossReasonsTable from "./LossReasonsTable";
import {useCardChannel} from "./useCardChannel";

const SHOWN = 10;

function LossReasonsCard({filters}: {filters: ReturnType<typeof useChannelsSummaryFilters>}) {
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("reasonperiod", pagePeriod);
  const channel = useCardChannel(filters, "reasonch", {shopsOnly: true});
  const [kind, setKind] = useSyncedWithQueryState<AnalyticsLossKind>(
    "reasonkind",
    (q) => (q === "cancellations" ? q : "returns"),
    (v) => (v === "cancellations" ? v : null),
  );
  const [subject, setSubject] = useSyncedWithQueryState<AnalyticsAbcSubject>(
    "reasonby",
    (q) => SUBJECTS.find((s) => s === q) ?? "catalogItem",
    (v) => (v === "catalogItem" ? null : v),
  );
  const [rankBy, setRankBy] = useSyncedWithQueryState<AnalyticsLossReasonsBy>(
    "reasonrank",
    (q) => (q === "share" ? q : "units"),
    (v) => (v === "share" ? v : null),
  );
  const [storedStep, setStep] = useSyncedWithQueryState("reasonstep", parseStep, (v) => v);
  const [showAll, setShowAll] = useSyncedWithQueryState(
    "reasonall",
    (q) => q === "1",
    (v) => (v ? "1" : null),
  );
  const step = storedStep ?? (selection.preset === "page" ? filters.step : null);
  const shopsSelected = filters.channelQuery.IncludeMarketplaces;

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsLossReasonsOptions({
      query: {
        ...filters.channelQuery,
        ...channel.query,
        From: period.from,
        To: period.to,
        Kind: kind,
        Subject: subject,
        By: rankBy,
        Step: step ?? undefined,
        Take: showAll ? undefined : SHOWN,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
    enabled: shopsSelected,
  });

  const isReturns = kind === "returns";
  const hasImmature = data?.immatureIntervals.some(Boolean);

  return (
    <SummaryCard
      title="Причины отмен и возвратов"
      hint="Какие товары и по каким причинам отменяли и возвращали чаще всего, и на какие интервалы пришлись эти заказы"
      isFetching={isFetching}
      subtitle={data && data.totalUnits > 0 && `${formatNumber(data.totalUnits)} шт.`}
      actions={
        <>
          <StepMeasureToggles step={data?.step ?? step} onStepChange={setStep} />
          <CardChannelSelect
            options={channel.options}
            value={channel.channel}
            onChange={channel.setChannel}
          />
          <PeriodPicker
            variant="compact"
            presets={CARD_PERIOD_PRESETS}
            value={selection}
            onChange={(value: PeriodSelection) => {
              if (value.preset !== selection.preset) setStep(null);
              setSelection(value);
            }}
            pagePeriod={pagePeriod}
          />
        </>
      }
    >
      <Stack spacing={1.5}>
        <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={kind}
            onChange={(_, value: AnalyticsLossKind | null) => value && setKind(value)}
          >
            <ToggleButton value="returns">Возвраты</ToggleButton>
            <ToggleButton value="cancellations">Отмены</ToggleButton>
          </ToggleButtonGroup>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={subject}
            onChange={(_, value: AnalyticsAbcSubject | null) => value && setSubject(value)}
          >
            {SUBJECTS.map((s) => (
              <Tooltip key={s} title={SUBJECT_TOOLTIPS[s]}>
                <ToggleButton value={s}>{SUBJECT_LABELS[s].toggle}</ToggleButton>
              </Tooltip>
            ))}
          </ToggleButtonGroup>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={rankBy}
            onChange={(_, value: AnalyticsLossReasonsBy | null) => value && setRankBy(value)}
          >
            <ToggleButton value="units">По штукам</ToggleButton>
            <Tooltip
              title={`Доля от ${isReturns ? "проданных" : "проданных и отменённых"} штук. Строки, где таких штук меньше ${data?.minShareBase ?? 10}, в рейтинг по доле не входят`}
            >
              <ToggleButton value="share">По доле</ToggleButton>
            </Tooltip>
          </ToggleButtonGroup>
        </Stack>

        {!shopsSelected ? (
          <Typography color="text.secondary">
            Причины есть только у магазинов — выберите хотя бы один в фильтре каналов
          </Typography>
        ) : (
          <>
            {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
            {data &&
              (data.rows.length === 0 ? (
                <Typography color="text.secondary">
                  {isReturns ? "Возвратов" : "Отмен"} по заказам периода нет
                </Typography>
              ) : (
                <LossReasonsTable data={data} />
              ))}
          </>
        )}

        {data && shopsSelected && (
          <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}>
            <Typography variant="caption" color="text.secondary" sx={{flex: "1 1 320px"}}>
              Всё привязано к дате заказа: {isReturns ? "возврат" : "отмена"} попадает в интервал,
              когда товар заказали.
              {isReturns && hasImmature && " Бледные столбики — возвраты ещё поступают."}
              {data.unlinkedUnits > 0 &&
                ` Без привязки: ${formatNumber(data.unlinkedUnits)} шт. — в рейтинг не вошли.`}
            </Typography>
            {(showAll || data.totalRows > data.rows.length) && (
              <Button size="small" onClick={() => setShowAll(!showAll)} sx={{ml: "auto"}}>
                {showAll ? "Свернуть" : `Весь список (${formatNumber(data.totalRows)})`}
              </Button>
            )}
          </Stack>
        )}
      </Stack>
    </SummaryCard>
  );
}

export default LossReasonsCard;
