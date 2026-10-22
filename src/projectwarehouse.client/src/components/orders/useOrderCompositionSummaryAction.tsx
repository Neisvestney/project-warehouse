import {useState} from "react";
import FunctionsIcon from "@mui/icons-material/Functions";
import type {BulkAction} from "@/components/BulkBar";
import OrderCompositionSummaryDialog from "./OrderCompositionSummaryDialog";

/** Bulk «Посчитать…» action; `dialogs` must be rendered by the caller. */
export function useOrderCompositionSummaryAction() {
  // captured on click, so the dialog sums what was selected when it opened
  const [dialogOrderIds, setDialogOrderIds] = useState<string[] | null>(null);

  function getAction(orderIds: string[]): BulkAction {
    return {
      key: "compositionSummary",
      label: "Посчитать…",
      icon: <FunctionsIcon />,
      disabled: orderIds.length === 0,
      onClick: () => setDialogOrderIds(orderIds),
    };
  }

  const dialogs = (
    <OrderCompositionSummaryDialog
      orderIds={dialogOrderIds}
      onClose={() => setDialogOrderIds(null)}
    />
  );

  return {getAction, dialogs};
}
