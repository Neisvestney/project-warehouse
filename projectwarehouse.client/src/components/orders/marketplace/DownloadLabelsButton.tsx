import {useState} from "react";
import {Button} from "@mui/material";
import LocalPrintshopIcon from "@mui/icons-material/LocalPrintshop";
import type {OrderLabelsGrouping} from "@/api/types.gen";
import DownloadLabelsDialog from "./DownloadLabelsDialog";
import LabelsErrorDialog from "./LabelsErrorDialog";
import {useDownloadLabels} from "./useDownloadLabels";

interface DownloadLabelsButtonProps {
  orderIds: string[];
}

function DownloadLabelsButton({orderIds}: DownloadLabelsButtonProps) {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const {download, isPending, error, clearError} = useDownloadLabels();

  async function handleConfirm(grouping: OrderLabelsGrouping) {
    // closed either way: on failure the error dialog takes over, and the choice is remembered
    await download({orderIds, grouping});
    setIsDialogOpen(false);
  }

  return (
    <>
      <Button
        size="small"
        variant="contained"
        color="inherit"
        startIcon={<LocalPrintshopIcon />}
        disabled={orderIds.length === 0}
        onClick={() => setIsDialogOpen(true)}
        sx={{color: "primary.main", bgcolor: "white"}}
      >
        Скачать этикетки
      </Button>
      <DownloadLabelsDialog
        open={isDialogOpen}
        isPending={isPending}
        onClose={() => setIsDialogOpen(false)}
        onConfirm={handleConfirm}
      />
      <LabelsErrorDialog error={error} onClose={clearError} />
    </>
  );
}

export default DownloadLabelsButton;
