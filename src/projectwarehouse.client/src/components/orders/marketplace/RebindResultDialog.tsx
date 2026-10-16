import {Button, Dialog, DialogActions, DialogContent, DialogTitle, Typography} from "@mui/material";
import type {BatchRebindResponse} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {resolveErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {formatOrderNumber} from "../orderUtils";

interface RebindResultDialogProps {
  result: BatchRebindResponse | null;
  error: string | null;
  onClose: () => void;
}

/** Outcome of a bulk rebind: counts, then the refused orders with their reasons. */
function RebindResultDialog({result, error, onClose}: RebindResultDialogProps) {
  const open = result != null || error != null;
  useBackClosable(open, onClose);
  const [shownResult, releaseResult] = useRetainedValue(result);
  const [shownError, releaseError] = useRetainedValue(error);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      fullWidth
      slotProps={{
        transition: {
          onExited: () => {
            releaseResult();
            releaseError();
          },
        },
      }}
    >
      <DialogTitle>Обновление привязки</DialogTitle>
      <DialogContent>
        {shownError ? (
          <Typography variant="body2" color="error">
            {shownError}
          </Typography>
        ) : (
          shownResult && (
            <>
              <Typography variant="body2">
                Обновлено: {pluralCount(shownResult.reboundOrderIds.length, NOUNS.order)}
              </Typography>
              <Typography variant="body2">
                Без изменений: {pluralCount(shownResult.unchangedOrderIds.length, NOUNS.order)}
              </Typography>
              {shownResult.failedItems.length > 0 && (
                <>
                  <Typography variant="body2" color="error" sx={{mt: 1, mb: 0.5}}>
                    Не удалось обновить:
                  </Typography>
                  {shownResult.failedItems.map((f) => (
                    <Typography key={f.orderId} variant="caption" sx={{display: "block"}}>
                      • {f.orderNumber != null ? formatOrderNumber(f.orderNumber) : "Заказ"}:{" "}
                      {resolveErrorMessage(f.error)}
                    </Typography>
                  ))}
                </>
              )}
            </>
          )
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Закрыть</Button>
      </DialogActions>
    </Dialog>
  );
}

export default RebindResultDialog;
