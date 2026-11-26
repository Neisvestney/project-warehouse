import {useState} from "react";
import LocalPrintshopIcon from "@mui/icons-material/LocalPrintshop";
import type {BulkAction} from "@/components/BulkBar";
import type {MarketplaceType} from "@/api/types.gen";
import DownloadLabelsDialog, {type LabelsPrintOptions} from "./DownloadLabelsDialog";
import LabelsErrorDialog from "./LabelsErrorDialog";
import {useDownloadLabels} from "./useDownloadLabels";

/** Bulk «Скачать этикетки» action; `dialogs` must be rendered by the caller. */
export function useDownloadLabelsAction() {
  // captured on click, so the dialog prints what was selected when it opened
  const [dialogOrderIds, setDialogOrderIds] = useState<string[] | null>(null);
  const [hasMixedMarketplaces, setHasMixedMarketplaces] = useState(false);
  const {download, isPending, error, clearError} = useDownloadLabels();

  async function handleConfirm(options: LabelsPrintOptions) {
    if (!dialogOrderIds) return;
    // closed either way: on failure the error dialog takes over, and the choice is remembered
    await download({orderIds: dialogOrderIds, ...options});
    setDialogOrderIds(null);
  }

  function getAction(
    orders: {id: string; marketplaceOrder?: {marketplaceType: MarketplaceType} | null}[],
  ): BulkAction {
    const orderIds = orders.map((order) => order.id);
    return {
      key: "downloadLabels",
      label: "Скачать этикетки",
      icon: <LocalPrintshopIcon />,
      primary: true,
      pending: isPending,
      disabled: orderIds.length === 0 || isPending,
      onClick: () => {
        setHasMixedMarketplaces(
          new Set(
            orders.flatMap((order) =>
              order.marketplaceOrder ? [order.marketplaceOrder.marketplaceType] : [],
            ),
          ).size > 1,
        );
        setDialogOrderIds(orderIds);
      },
    };
  }

  const dialogs = (
    <>
      <DownloadLabelsDialog
        open={dialogOrderIds != null}
        isPending={isPending}
        hasMixedMarketplaces={hasMixedMarketplaces}
        onClose={() => setDialogOrderIds(null)}
        onConfirm={handleConfirm}
      />
      <LabelsErrorDialog error={error} onClose={clearError} />
    </>
  );

  return {getAction, dialogs};
}
