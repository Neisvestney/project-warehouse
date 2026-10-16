import {Stack, Typography} from "@mui/material";
import type {MarketplaceLossReason} from "@/api/types.gen";
import {formatLossReason} from "./marketplaceOrderUtils";

interface LossReasonTextProps {
  category: MarketplaceLossReason | null | undefined;
  wording: string | null | undefined;
}

/** The normalized reason with the marketplace's own wording beneath it. */
export default function LossReasonText({category, wording}: LossReasonTextProps) {
  if (!category) return wording ?? null;
  return (
    <Stack component="span">
      <span>{formatLossReason(category)}</span>
      {wording && (
        <Typography component="span" variant="caption" color="text.secondary">
          {wording}
        </Typography>
      )}
    </Stack>
  );
}
