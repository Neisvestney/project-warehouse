import {Button, Tooltip} from "@mui/material";
import LocalPrintshopIcon from "@mui/icons-material/LocalPrintshop";
import type {MarketplaceOrderDetailsDto} from "@/api/types.gen";
import LabelsErrorDialog from "./LabelsErrorDialog";
import {useDownloadLabels} from "./useDownloadLabels";

interface DownloadOrderLabelButtonProps {
  orderId: string;
  marketplaceOrder: MarketplaceOrderDetailsDto;
}

/** Single order — grouping has nothing to group, so the dialog is skipped. */
function DownloadOrderLabelButton({orderId, marketplaceOrder}: DownloadOrderLabelButtonProps) {
  const {download, isPending, error, clearError} = useDownloadLabels();

  // stored labels reprint at any status; a missing one the marketplace only prints while awaiting shipment
  const isCached =
    marketplaceOrder.parcels.length > 0 &&
    marketplaceOrder.parcels.every((p) => p.labelFileId != null);
  const canDownload = isCached || marketplaceOrder.status === "awaitingDeliver";

  const button = (
    <Button
      variant="outlined"
      startIcon={<LocalPrintshopIcon />}
      disabled={!canDownload || isPending}
      loading={isPending}
      onClick={() =>
        download({
          orderIds: [orderId],
          fileName: `label-${marketplaceOrder.postingNumber}.pdf`,
        })
      }
    >
      Скачать этикетку
    </Button>
  );

  return (
    <>
      {canDownload ? (
        button
      ) : (
        <Tooltip title="Этикетка ещё не скачана, а отправление не в статусе «ожидает отгрузки»">
          <span>{button}</span>
        </Tooltip>
      )}
      <LabelsErrorDialog error={error} onClose={clearError} />
    </>
  );
}

export default DownloadOrderLabelButton;
