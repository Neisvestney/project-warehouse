import {useState} from "react";
import {Alert, Button, CircularProgress, Snackbar} from "@mui/material";
import LocalPrintshopIcon from "@mui/icons-material/LocalPrintshop";
import {ordersGetLabels} from "@/api/sdk.gen";
import {saveBlob} from "@/utils/downloadUtils";
import {parseProblemFromBlob} from "@/utils/blobErrorUtils";
import {extractErrorMessage, firstFieldError, resolveErrorMessage} from "@/utils/errorUtils";

interface DownloadLabelsButtonProps {
  orderIds: string[];
}

function DownloadLabelsButton({orderIds}: DownloadLabelsButtonProps) {
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    setIsPending(true);
    setError(null);
    try {
      // The generated *Options helpers mistype binary endpoints, so the SDK function is called
      // directly with parseAs: "blob" — same approach as useFileBlobUrl.
      const response = await ordersGetLabels({
        body: {orderIds},
        parseAs: "blob",
        throwOnError: false,
      });

      if (response.error !== undefined) {
        const problem = (await parseProblemFromBlob(response.error)) ?? response.error;
        const fieldError =
          typeof problem === "object" && problem !== null && "errors" in problem
            ? firstFieldError(problem as never)
            : undefined;
        setError(fieldError ? resolveErrorMessage(fieldError) : extractErrorMessage(problem));
        return;
      }

      saveBlob(response.data as unknown as Blob, "labels.pdf");
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setIsPending(false);
    }
  }

  return (
    <>
      <Button
        size="small"
        variant="contained"
        color="inherit"
        startIcon={
          isPending ? <CircularProgress size={14} color="inherit" /> : <LocalPrintshopIcon />
        }
        disabled={isPending || orderIds.length === 0}
        onClick={handleClick}
        sx={{color: "primary.main", bgcolor: "white"}}
      >
        Скачать этикетки
      </Button>
      <Snackbar
        open={error !== null}
        autoHideDuration={8000}
        onClose={() => setError(null)}
        anchorOrigin={{vertical: "bottom", horizontal: "center"}}
      >
        <Alert severity="warning" onClose={() => setError(null)}>
          {error}
        </Alert>
      </Snackbar>
    </>
  );
}

export default DownloadLabelsButton;
