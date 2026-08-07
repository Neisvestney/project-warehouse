import {Stack, Typography} from "@mui/material";
import type {SkippedOrderInfo} from "@/api/types.gen";
import {resolveErrorMessage} from "@/utils/errorUtils";

interface SkippedOrdersListProps {
  items: SkippedOrderInfo[];
  /** Total from the run — larger than items.length once the 100-entry cap kicks in. */
  total: number;
}

function SkippedOrdersList({items, total}: SkippedOrdersListProps) {
  if (items.length === 0) return null;

  return (
    <Stack spacing={0.5}>
      <Typography variant="subtitle2">Не импортированы</Typography>
      {items.map((item) => (
        <Typography key={item.postingNumber} variant="caption" sx={{display: "block"}}>
          • <b>{item.postingNumber}</b>:{" "}
          {resolveErrorMessage({
            code: item.reason,
            detail: "",
            args: {offerIds: item.offerIds.join(", ")},
          })}
        </Typography>
      ))}
      {total > items.length && (
        <Typography variant="caption" color="text.secondary">
          Показаны первые {items.length} из {total}
        </Typography>
      )}
    </Stack>
  );
}

export default SkippedOrdersList;
