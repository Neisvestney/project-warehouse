import {Box, Stack, Tooltip, Typography} from "@mui/material";
import type {PayoutsAppliedSettingsDto} from "@/api/types.gen";
import {ageBucketLabels, ageBucketStarts, formatEstimate} from "./payoutsFormat";

interface PayoutsAgeBarProps {
  byAge: number[];
  currencyCode: string;
  settings: PayoutsAppliedSettingsDto;
}

/** «В пути» split by age: one stacked bar with a legend, the overdue buckets in red. */
function PayoutsAgeBar({byAge, currencyCode, settings}: PayoutsAgeBarProps) {
  const labels = ageBucketLabels(settings.payoutAgeBoundaries);
  const starts = ageBucketStarts(settings.payoutAgeBoundaries);
  const total = byAge.reduce((acc, v) => acc + v, 0);
  if (byAge.length === 0 || total <= 0) return null;

  const buckets = byAge.map((amount, i) => {
    const overdue = starts[i] >= settings.payoutOverdueDays;
    return {
      label: labels[i],
      amount,
      color: overdue ? "error.main" : "primary.main",
      // Fresh buckets are lighter so the bar reads as ageing left to right
      opacity: overdue ? 1 : 0.35 + (0.5 * i) / Math.max(byAge.length - 1, 1),
    };
  });

  return (
    <Stack spacing={0.75}>
      <Typography variant="caption" color="text.secondary">
        В пути по возрасту
      </Typography>
      <Box
        sx={{
          display: "flex",
          height: 8,
          borderRadius: 1,
          overflow: "hidden",
          bgcolor: "action.hover",
        }}
      >
        {buckets.map(
          (b) =>
            b.amount > 0 && (
              <Tooltip
                key={b.label}
                title={`${b.label}: ${formatEstimate(b.amount, currencyCode)}`}
              >
                <Box sx={{flex: `${b.amount / total} 1 0`, bgcolor: b.color, opacity: b.opacity}} />
              </Tooltip>
            ),
        )}
      </Box>
      <Box sx={{display: "flex", flexWrap: "wrap", columnGap: 1.5, rowGap: 0.25}}>
        {buckets.map((b) => (
          <Stack key={b.label} direction="row" spacing={0.5} sx={{alignItems: "center"}}>
            <Box
              sx={{width: 8, height: 8, borderRadius: "50%", bgcolor: b.color, opacity: b.opacity}}
            />
            <Typography
              variant="caption"
              color={b.color === "error.main" && b.amount > 0 ? "error.main" : "text.secondary"}
              sx={{whiteSpace: "nowrap"}}
            >
              {b.label} {formatEstimate(b.amount, currencyCode)}
            </Typography>
          </Stack>
        ))}
      </Box>
    </Stack>
  );
}

export default PayoutsAgeBar;
