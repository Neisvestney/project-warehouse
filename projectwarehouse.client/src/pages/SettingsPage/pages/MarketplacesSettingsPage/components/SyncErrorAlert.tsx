import {Alert, AlertTitle, Typography} from "@mui/material";
import type {AppFieldError} from "@/api/types.gen";
import {resolveErrorMessage} from "@/utils/errorUtils";

interface SyncErrorAlertProps {
  error: AppFieldError | null | undefined;
  title?: string;
}

function SyncErrorAlert({
  error,
  title = "Последняя синхронизация завершилась ошибкой",
}: SyncErrorAlertProps) {
  if (!error) return null;

  const response = error.args?.["marketplaceResponse"];

  return (
    <Alert severity="error" sx={{my: 2}}>
      <AlertTitle>{title}</AlertTitle>
      {resolveErrorMessage(error)}
      {typeof response === "string" && response && (
        <Typography
          variant="caption"
          component="pre"
          sx={{mt: 1, whiteSpace: "pre-wrap", wordBreak: "break-all", fontFamily: "monospace"}}
        >
          {response}
        </Typography>
      )}
    </Alert>
  );
}

export default SyncErrorAlert;
