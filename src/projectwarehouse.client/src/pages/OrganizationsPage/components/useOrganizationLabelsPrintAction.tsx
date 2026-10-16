import {useState} from "react";
import PrintIcon from "@mui/icons-material/Print";
import type {BulkAction} from "@/components/BulkBar";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {
  OrganizationLabelsPrintDialog,
  type OrganizationLabelItem,
} from "./OrganizationLabelsPrintDialog";

/** Bulk «Этикетки» action over selected organizations; `dialogs` must be rendered by the caller. */
export function useOrganizationLabelsPrintAction() {
  // captured on click, so the dialog prints what was selected when it opened
  const [dialogItems, setDialogItems] = useState<OrganizationLabelItem[] | null>(null);
  const [shownItems] = useRetainedValue(dialogItems);

  function getAction(items: OrganizationLabelItem[]): BulkAction {
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
    <OrganizationLabelsPrintDialog
      items={shownItems ?? []}
      open={dialogItems != null}
      onClose={() => setDialogItems(null)}
    />
  );

  return {getAction, dialogs};
}
