import {useState} from "react";
import LocalPrintshopIcon from "@mui/icons-material/LocalPrintshop";
import type {OrderLabelsGrouping} from "@/api/types.gen";
import type {BulkAction} from "@/components/BulkBar";
import DownloadLabelsDialog from "./DownloadLabelsDialog";
import LabelsErrorDialog from "./LabelsErrorDialog";
import {useDownloadLabels} from "./useDownloadLabels";

/** Bulk «Скачать этикетки» action; `dialogs` must be rendered by the caller. */
export function useDownloadLabelsAction() {
  // captured on click, so the dialog prints what was selected when it opened
  const [dialogOrderIds, setDialogOrderIds] = useState<string[] | null>(null);
  const {download, isPending, error, clearError} = useDownloadLabels();

  async function handleConfirm(grouping: OrderLabelsGrouping, forceRegenerate: boolean) {
    if (!dialogOrderIds) return;
    // closed either way: on failure the error dialog takes over, and the choice is remembered
    await download({orderIds: dialogOrderIds, grouping, forceRegenerate});
    setDialogOrderIds(null);
  }

  function getAction(orderIds: string[]): BulkAction {
    return {
      key: "downloadLabels",
      label: "Скачать этикетки",
      icon: <LocalPrintshopIcon />,
      primary: true,
      pending: isPending,
      disabled: orderIds.length === 0 || isPending,
      onClick: () => setDialogOrderIds(orderIds),
    };
  }

  const dialogs = (
    <>
      <DownloadLabelsDialog
        open={dialogOrderIds != null}
        isPending={isPending}
        onClose={() => setDialogOrderIds(null)}
        onConfirm={handleConfirm}
      />
      <LabelsErrorDialog error={error} onClose={clearError} />
    </>
  );

  return {getAction, dialogs};
}
