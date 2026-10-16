import CloseIcon from "@mui/icons-material/Close";
import {Alert, Button, IconButton, Stack} from "@mui/material";
import type {StaleActor} from "@/hooks/useStaleData";

interface StaleDataBannerProps {
  isStale: boolean;
  staleBy: StaleActor | null;
  onRefresh: () => void;
  onDismiss: () => void;
}

/**
 * Only shown for a modified form — an untouched one is refreshed silently. Refreshing is left to the
 * user because it replaces whatever they have already typed.
 */
function StaleDataBanner({isStale, staleBy, onRefresh, onDismiss}: StaleDataBannerProps) {
  if (!isStale) return null;

  return (
    <Alert
      severity="info"
      action={
        <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
          <Button color="inherit" size="small" onClick={onRefresh}>
            Обновить
          </Button>
          <IconButton color="inherit" size="small" onClick={onDismiss} aria-label="Скрыть">
            <CloseIcon fontSize="inherit" />
          </IconButton>
        </Stack>
      }
    >
      {staleBy ? `${staleBy.userName} сохранил изменения. ` : ""}Данные на экране могли устареть
    </Alert>
  );
}

export default StaleDataBanner;
