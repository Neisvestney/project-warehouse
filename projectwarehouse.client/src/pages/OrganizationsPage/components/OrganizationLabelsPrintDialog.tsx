import {useState} from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import PrintIcon from "@mui/icons-material/Print";
import type {OrganizationSummaryDto} from "@/api/types.gen";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {useBackClosable} from "@/hooks/useBackClosable";
import {formatEntityBarcode} from "@/utils/barcodeUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {openPrintPage, type PrintItem} from "@/utils/printUtils";

export type OrganizationLabelItem = Pick<OrganizationSummaryDto, "id" | "name" | "accounts">;

function toPrintItem(item: OrganizationLabelItem): PrintItem {
  const accountNames = item.accounts.map((a) => a.name).join(", ");
  return {
    type: "DataMatrix",
    value: formatEntityBarcode("organization", item.id),
    label: accountNames ? `${accountNames}\n${item.name}` : item.name,
  };
}

interface OrganizationLabelsPrintDialogProps {
  items: OrganizationLabelItem[];
  open: boolean;
  onClose: () => void;
}

/** Text-layout labels for organizations — account names as the title, the organization below; opens `/print`. */
export function OrganizationLabelsPrintDialog({
  items,
  open,
  onClose,
}: OrganizationLabelsPrintDialogProps) {
  const [copies, setCopies] = useState(1);
  const [prevOpen, setPrevOpen] = useState(open);

  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) setCopies(1);
  }

  useBackClosable(open, onClose);

  const single = items.length === 1;

  const handlePrint = () => {
    const printItems = items.flatMap((item) => {
      const printItem = toPrintItem(item);
      return Array.from({length: copies}, () => printItem);
    });
    openPrintPage(printItems, "text");
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{single ? "Печать этикетки" : "Печать этикеток"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          {!single && (
            <Typography variant="body2">
              Выбрано: {pluralCount(items.length, NOUNS.organization)}.
            </Typography>
          )}
          <ClampedIntegerField
            label={single ? "Количество копий" : "Копий на организацию"}
            size="small"
            value={copies}
            max={200}
            onCommit={setCopies}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Отмена</Button>
        <Button variant="contained" startIcon={<PrintIcon />} onClick={handlePrint}>
          Печать
        </Button>
      </DialogActions>
    </Dialog>
  );
}
