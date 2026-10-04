import {useState} from "react";
import PrintIcon from "@mui/icons-material/Print";
import type {BulkAction} from "@/components/BulkBar";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {CatalogLabelsPrintDialog, type CatalogLabelItem} from "./CatalogLabelsPrintDialog";

/** Bulk «Этикетки» action over selected catalog items; `dialogs` must be rendered by the caller. */
export function useCatalogLabelsPrintAction() {
  // captured on click, so the dialog prints what was selected when it opened
  const [dialogItems, setDialogItems] = useState<CatalogLabelItem[] | null>(null);
  // keeps the content on screen through the closing animation
  const [shownItems] = useRetainedValue(dialogItems);

  function getAction(items: CatalogLabelItem[]): BulkAction {
    return {
      key: "printLabels",
      label: "Этикетки",
      icon: <PrintIcon />,
      count: items.length,
      primary: true,
      disabled: items.length === 0,
      onClick: () => setDialogItems(items),
    };
  }

  const dialogs = (
    <CatalogLabelsPrintDialog
      items={shownItems ?? []}
      open={dialogItems != null}
      onClose={() => setDialogItems(null)}
    />
  );

  return {getAction, dialogs};
}
